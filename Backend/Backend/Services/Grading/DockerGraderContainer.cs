using Docker.DotNet;
using Docker.DotNet.Models;
using System.Net;

namespace Backend.Services.Grading;

internal sealed class DockerGraderContainer
{
    private const string ImageRepository = "ojsharp-grader";
    private const string ImageVersionTag = "python-js-ts-v8";
    private const string ImageTag = ImageRepository + ":" + ImageVersionTag;
    private const string ContainerName = "ojsharp-grader-python-js-ts-v8";
    private const string DefaultUnixEndpoint = "unix:///var/run/docker.sock";
    private const string DefaultWindowsEndpoint = "npipe://./pipe/docker_engine";
    private const int ContainerLifecycleRetryCount = 8;
    private static readonly TimeSpan ContainerLifecycleRetryDelay = TimeSpan.FromMilliseconds(750);
    private static readonly SemaphoreSlim ContainerLifecycleGate = new(1, 1);
    private readonly DockerClient dockerClient;
    private readonly string workspaceHostRoot;
    private string? containerId;

    public DockerGraderContainer()
        : this(CreateDockerClient(), new GradingWorkspace().HostRoot)
    {
    }

    public DockerGraderContainer(DockerClient dockerClient, string workspaceHostRoot)
    {
        this.dockerClient = dockerClient;
        this.workspaceHostRoot = workspaceHostRoot;
    }

    public async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        await ContainerLifecycleGate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(workspaceHostRoot);
            await EnsureImageAsync(cancellationToken);
            for (var attempt = 0; attempt <= ContainerLifecycleRetryCount; attempt++)
            {
                try
                {
                    containerId = await EnsureContainerAsync(cancellationToken);
                    return;
                }
                catch (DockerApiException exception) when (IsContainerLifecycleConflict(exception)
                                                          && attempt < ContainerLifecycleRetryCount)
                {
                    containerId = null;
                    await Task.Delay(ContainerLifecycleRetryDelay, cancellationToken);
                }
            }
        }
        finally
        {
            ContainerLifecycleGate.Release();
        }
    }

    public async Task<DockerExecResult> ExecuteAsync(
        string workingDirectory,
        IReadOnlyList<string> command,
        TimeSpan hostTimeout,
        CancellationToken cancellationToken)
    {
        await EnsureReadyAsync(cancellationToken);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(hostTimeout);

        try
        {
            var createResponse = await dockerClient.Exec.ExecCreateContainerAsync(
                containerId!,
                new ContainerExecCreateParameters
                {
                    AttachStderr = true,
                    AttachStdout = true,
                    Cmd = command.ToList(),
                    WorkingDir = workingDirectory
                },
                cancellationToken);

            using var stream = await dockerClient.Exec.StartAndAttachContainerExecAsync(
                createResponse.ID,
                false,
                timeoutSource.Token);
            var output = await stream.ReadOutputToEndAsync(timeoutSource.Token);
            var inspect = await dockerClient.Exec.InspectContainerExecAsync(createResponse.ID, cancellationToken);
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

    private async Task<string> EnsureContainerAsync(CancellationToken cancellationToken)
    {
        var container = await FindContainerAsync(cancellationToken);
        if (container is null)
        {
            return await CreateContainerAsync(cancellationToken);
        }

        if (!HasExpectedWorkspaceBind(container))
        {
            if (container.State.Equals("running", StringComparison.OrdinalIgnoreCase))
            {
                await dockerClient.Containers.StopContainerAsync(
                    container.ID,
                    new ContainerStopParameters(),
                    cancellationToken);
            }

            await dockerClient.Containers.RemoveContainerAsync(
                container.ID,
                new ContainerRemoveParameters { Force = true },
                cancellationToken);
            await WaitForContainerRemovalAsync(cancellationToken);
            return await CreateContainerAsync(cancellationToken);
        }

        if (!container.State.Equals("running", StringComparison.OrdinalIgnoreCase))
        {
            await dockerClient.Containers.StartContainerAsync(
                container.ID,
                new ContainerStartParameters(),
                cancellationToken);
        }

        return container.ID;
    }

    private async Task<string> CreateContainerAsync(CancellationToken cancellationToken)
    {
        var createResponse = await dockerClient.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = ImageTag,
                Name = ContainerName,
                Cmd = ["sleep", "infinity"],
                NetworkDisabled = true,
                HostConfig = new HostConfig
                {
                    AutoRemove = false,
                    Binds = [$"{workspaceHostRoot}:/workspace"],
                    Memory = 256 * 1024 * 1024,
                    NanoCPUs = 1000000000,
                    CapDrop = new[] { "ALL" }
                }
            },
            cancellationToken);
        await dockerClient.Containers.StartContainerAsync(
            createResponse.ID,
            new ContainerStartParameters(),
            cancellationToken);
        return createResponse.ID;
    }

    private async Task<ContainerListResponse?> FindContainerAsync(CancellationToken cancellationToken)
    {
        var containers = await dockerClient.Containers.ListContainersAsync(
            new ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["name"] = new Dictionary<string, bool> { [ContainerName] = true }
                }
            },
            cancellationToken);

        return containers.FirstOrDefault(container =>
            container.Names.Any(name => name.TrimStart('/').Equals(ContainerName, StringComparison.Ordinal)));
    }

    private async Task WaitForContainerRemovalAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < ContainerLifecycleRetryCount; attempt++)
        {
            if (await FindContainerAsync(cancellationToken) is null)
            {
                return;
            }

            await Task.Delay(ContainerLifecycleRetryDelay, cancellationToken);
        }
    }

    private bool HasExpectedWorkspaceBind(ContainerListResponse container)
    {
        return container.Mounts.Any(mount =>
            string.Equals(mount.Source, workspaceHostRoot, StringComparison.Ordinal)
            && string.Equals(mount.Destination, "/workspace", StringComparison.Ordinal));
    }

    private static bool IsContainerLifecycleConflict(DockerApiException exception)
    {
        if (exception.StatusCode != HttpStatusCode.Conflict
            && exception.StatusCode != HttpStatusCode.NotFound)
        {
            return false;
        }

        var message = exception.ResponseBody ?? exception.Message;
        return message.Contains(ContainerName, StringComparison.OrdinalIgnoreCase)
               || message.Contains("removal of container", StringComparison.OrdinalIgnoreCase)
               || message.Contains("already in progress", StringComparison.OrdinalIgnoreCase)
               || message.Contains("is not running", StringComparison.OrdinalIgnoreCase);
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
