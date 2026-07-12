using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace Backend.Services.Grading;

internal sealed class DockerGraderContainer
{
    private const string ImageRepository = "ojsharp-grader";
    private const string ImageVersionTag = "python-js-ts-v8";
    private const string ImageTag = ImageRepository + ":" + ImageVersionTag;
    private const string ContainerWorkspace = "/workspace";
    private const string DefaultUnixEndpoint = "unix:///var/run/docker.sock";
    private const string DefaultWindowsEndpoint = "npipe://./pipe/docker_engine";
    private const int MaximumConcurrentExecutions = 4;
    private const long ExecutionMemoryBytes = 384L * 1024 * 1024;
    private const long ExecutionNanoCpus = 1_500_000_000;
    private static readonly TimeSpan ImageReadyWaitTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(2);
    private static readonly SemaphoreSlim ExecutionGate = new(MaximumConcurrentExecutions, MaximumConcurrentExecutions);
    private readonly DockerClient dockerClient;
    private readonly ILogger<DockerGraderContainer> logger;
    private readonly object readinessLock = new();
    private Task? readinessTask;

    public DockerGraderContainer()
        : this(CreateDockerClient(), NullLogger<DockerGraderContainer>.Instance)
    {
    }

    public DockerGraderContainer(ILogger<DockerGraderContainer> logger)
        : this(CreateDockerClient(), logger)
    {
    }

    internal DockerGraderContainer(
        DockerClient dockerClient,
        ILogger<DockerGraderContainer> logger)
    {
        this.dockerClient = dockerClient;
        this.logger = logger;
    }

    public bool IsReady
    {
        get
        {
            lock (readinessLock)
            {
                return readinessTask?.IsCompletedSuccessfully == true;
            }
        }
    }

    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        var task = GetOrStartReadinessTask();
        try
        {
            await task.WaitAsync(cancellationToken);
        }
        catch
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                lock (readinessLock)
                {
                    if (ReferenceEquals(readinessTask, task))
                    {
                        readinessTask = null;
                    }
                }
            }
            throw;
        }
    }

    public async Task<DockerExecResult> ExecuteAsync(
        string hostWorkspacePath,
        IReadOnlyList<string> command,
        TimeSpan hostTimeout,
        CancellationToken cancellationToken)
    {
        using (var readinessSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            readinessSource.CancelAfter(ImageReadyWaitTimeout);
            try
            {
                await EnsureReadyAsync(readinessSource.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new DockerExecResult(
                    string.Empty,
                    "Grader container unavailable: Sandbox grader is still warming up. Retry shortly.",
                    1,
                    false);
            }
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(hostTimeout);
        var executionSlotAcquired = false;
        string? containerReference = null;

        try
        {
            await ExecutionGate.WaitAsync(timeoutSource.Token);
            executionSlotAcquired = true;
            containerReference = $"ojsharp-run-{Guid.NewGuid():N}";
            var containerId = await CreateExecutionContainerAsync(
                hostWorkspacePath,
                containerReference,
                timeoutSource.Token);
            containerReference = containerId;
            await dockerClient.Containers.StartContainerAsync(
                containerId,
                new ContainerStartParameters(),
                timeoutSource.Token);

            var createResponse = await dockerClient.Exec.ExecCreateContainerAsync(
                containerId,
                new ContainerExecCreateParameters
                {
                    AttachStderr = true,
                    AttachStdout = true,
                    Cmd = BuildStagedCommand(command).ToList(),
                    WorkingDir = ContainerWorkspace
                },
                timeoutSource.Token);

            using var stream = await dockerClient.Exec.StartAndAttachContainerExecAsync(
                createResponse.ID,
                false,
                timeoutSource.Token);
            var output = await stream.ReadOutputToEndAsync(timeoutSource.Token);
            var inspect = await dockerClient.Exec.InspectContainerExecAsync(createResponse.ID, timeoutSource.Token);
            var exitCode = inspect.ExitCode;

            return new DockerExecResult(
                output.stdout,
                output.stderr,
                (int)exitCode,
                exitCode == 124);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new DockerExecResult(string.Empty, "Execution timed out.", 1, true);
        }
        finally
        {
            try
            {
                if (containerReference is not null)
                {
                    await RemoveExecutionContainerAsync(containerReference);
                }
            }
            finally
            {
                if (executionSlotAcquired)
                {
                    ExecutionGate.Release();
                }
            }
        }
    }

    private Task GetOrStartReadinessTask()
    {
        lock (readinessLock)
        {
            return readinessTask ??= EnsureImageAsync(CancellationToken.None);
        }
    }

    private async Task EnsureImageAsync(CancellationToken cancellationToken)
    {
        var images = await dockerClient.Images.ListImagesAsync(
            new ImagesListParameters
            {
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["reference"] = new Dictionary<string, bool> { [ImageTag] = true }
                }
            },
            cancellationToken);

        if (images.Any(image => image.RepoTags?.Contains(ImageTag) == true))
        {
            return;
        }

        var buildStartedAt = DateTime.UtcNow;
        await using var buildContext = DockerBuildContextFactory.Create();
        var buildErrors = new List<string>();
        var progress = new Progress<JSONMessage>(message =>
        {
            if (!string.IsNullOrWhiteSpace(message.ErrorMessage))
            {
                buildErrors.Add(message.ErrorMessage);
            }
        });

        await dockerClient.Images.BuildImageFromDockerfileAsync(
            new ImageBuildParameters
            {
                Dockerfile = "Dockerfile",
                ForceRemove = true,
                Remove = true,
                Tags = [ImageTag]
            },
            buildContext,
            null,
            new Dictionary<string, string>(),
            progress,
            cancellationToken);

        if (buildErrors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, buildErrors));
        }

        await TagBuiltImageIfNeededAsync(buildStartedAt, cancellationToken);
    }

    private async Task<string> CreateExecutionContainerAsync(
        string hostWorkspacePath,
        string containerName,
        CancellationToken cancellationToken)
    {
        var response = await dockerClient.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = ImageTag,
                Name = containerName,
                Cmd = ["sleep", "infinity"],
                NetworkDisabled = true,
                WorkingDir = ContainerWorkspace,
                Labels = new Dictionary<string, string>
                {
                    ["ojsharp.execution"] = "true",
                    ["ojsharp.workspace"] = Path.GetFileName(hostWorkspacePath)
                },
                HostConfig = new HostConfig
                {
                    AutoRemove = false,
                    Binds = [$"{hostWorkspacePath}:{ContainerWorkspace}"],
                    Memory = ExecutionMemoryBytes,
                    NanoCPUs = ExecutionNanoCpus,
                    PidsLimit = 128,
                    CapDrop = ["ALL"],
                    SecurityOpt = ["no-new-privileges"]
                }
            },
            cancellationToken);
        return response.ID;
    }

    private async Task RemoveExecutionContainerAsync(string containerId)
    {
        for (var attempt = 1; attempt <= 2; attempt += 1)
        {
            using var cleanupSource = new CancellationTokenSource(CleanupTimeout);
            try
            {
                await dockerClient.Containers.RemoveContainerAsync(
                    containerId,
                    new ContainerRemoveParameters { Force = true, RemoveVolumes = true },
                    cleanupSource.Token);
                return;
            }
            catch (DockerApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
            {
                return;
            }
            catch (Exception exception) when (attempt < 2)
            {
                logger.LogDebug(exception, "Retrying cleanup for sandbox container {ContainerId}.", containerId);
                await Task.Delay(100);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Could not remove sandbox container {ContainerId} after execution.", containerId);
            }
        }
    }

    private static IReadOnlyList<string> BuildStagedCommand(IReadOnlyList<string> command)
    {
        var trustedCommand = string.Join(" ", command.Select(ShellQuote));
        return
        [
            "sh",
            "-c",
            $$"""
            source={{ShellQuote(ContainerWorkspace)}}
            staged=$(mktemp -d /tmp/ojsharp-run-XXXXXX) || exit 1
            cleanup() { rm -rf "$staged"; }
            trap cleanup EXIT HUP INT TERM
            cp -a "$source/." "$staged/" || exit 1
            cd "$staged" || exit 1
            {{trustedCommand}}
            status=$?
            if [ -f actual.txt ]; then
              cp actual.txt "$source/actual.txt"
            fi
            exit "$status"
            """
        ];
    }

    private static string ShellQuote(string value)
    {
        return $"'{value.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'";
    }

    private async Task TagBuiltImageIfNeededAsync(DateTime buildStartedAt, CancellationToken cancellationToken)
    {
        var images = await dockerClient.Images.ListImagesAsync(
            new ImagesListParameters { All = true },
            cancellationToken);
        if (images.Any(image => image.RepoTags?.Contains(ImageTag) == true))
        {
            return;
        }

        var newestUntaggedBuildImage = images
            .Where(image => image.Created >= buildStartedAt.AddMinutes(-1) && IsDangling(image))
            .OrderByDescending(image => image.Created)
            .FirstOrDefault();
        if (newestUntaggedBuildImage is null)
        {
            throw new InvalidOperationException($"Docker build completed, but image '{ImageTag}' was not created.");
        }

        await dockerClient.Images.TagImageAsync(
            newestUntaggedBuildImage.ID,
            new ImageTagParameters
            {
                RepositoryName = ImageRepository,
                Tag = ImageVersionTag,
                Force = true
            },
            cancellationToken);
    }

    private static bool IsDangling(ImagesListResponse image)
    {
        return image.RepoTags is null
               || image.RepoTags.Count == 0
               || image.RepoTags.All(tag => tag.Equals("<none>:<none>", StringComparison.Ordinal));
    }

    internal static DockerClient CreateDockerClient()
    {
        return CreateDockerClient(ResolveDockerEndpoint(
            Environment.GetEnvironmentVariable("DOCKER_HOST"),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            OperatingSystem.IsWindows(),
            Path.Exists));
    }

    internal static DockerClient CreateDockerClient(string endpoint)
    {
        return new DockerClientConfiguration(new Uri(endpoint)).CreateClient();
    }

    internal static string ResolveDockerEndpoint(
        string? dockerHost,
        string? homeDirectory,
        bool isWindows,
        Func<string, bool> pathExists)
    {
        return TryResolveDockerEndpoint(dockerHost, homeDirectory, isWindows, pathExists)
               ?? (isWindows ? DefaultWindowsEndpoint : DefaultUnixEndpoint);
    }

    internal static string? TryResolveDockerEndpoint(
        string? dockerHost,
        string? homeDirectory,
        bool isWindows,
        Func<string, bool> pathExists)
    {
        var normalizedDockerHost = NormalizeDockerHost(dockerHost);
        if (!string.IsNullOrWhiteSpace(normalizedDockerHost))
        {
            return normalizedDockerHost;
        }

        if (isWindows)
        {
            return DefaultWindowsEndpoint;
        }

        var candidates = BuildUnixSocketCandidates(homeDirectory);
        var socketPath = candidates.FirstOrDefault(pathExists);
        return socketPath is null ? null : $"unix://{socketPath}";
    }

    internal static string? NormalizeDockerHost(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("npipe:", StringComparison.OrdinalIgnoreCase))
        {
            return value?.Trim();
        }

        var trimmed = value.Trim();
        var pipeIndex = trimmed.IndexOf("pipe/", StringComparison.OrdinalIgnoreCase);
        if (pipeIndex < 0)
        {
            return trimmed;
        }

        return $"npipe://./{trimmed[pipeIndex..]}";
    }

    private static IReadOnlyList<string> BuildUnixSocketCandidates(string? homeDirectory)
    {
        var candidates = new List<string> { "/var/run/docker.sock" };
        if (!string.IsNullOrWhiteSpace(homeDirectory))
        {
            candidates.Add(Path.Combine(homeDirectory, ".docker", "run", "docker.sock"));
            candidates.Add(Path.Combine(homeDirectory, ".docker", "desktop", "docker.sock"));
            candidates.Add(Path.Combine(homeDirectory, ".colima", "default", "docker.sock"));
        }

        return candidates;
    }
}

internal sealed record DockerExecResult(string Stdout, string Stderr, int ExitCode, bool TimedOut);
