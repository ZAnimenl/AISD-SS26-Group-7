using Docker.DotNet;
using Docker.DotNet.Models;

namespace Backend.Services.Grading;

internal sealed class DockerGraderContainer
{
    private const string ImageTag = "ojsharp-grader:python-js-ts-v1";
    private const string ContainerName = "ojsharp-grader-python-js-ts-v1";
    private readonly DockerClient dockerClient;
    private readonly string workspaceHostRoot;
    private readonly SemaphoreSlim gate = new(1, 1);
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
        await gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(workspaceHostRoot);
            await EnsureImageAsync(cancellationToken);
            containerId = await EnsureContainerAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
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
    }

    private async Task<string> EnsureContainerAsync(CancellationToken cancellationToken)
    {
        var container = await FindContainerAsync(cancellationToken);
        if (container is null)
        {
            var createResponse = await dockerClient.Containers.CreateContainerAsync(
                new CreateContainerParameters
                {
                    Image = ImageTag,
                    Name = ContainerName,
                    Cmd = ["sleep", "infinity"],
                    HostConfig = new HostConfig
                    {
                        AutoRemove = false,
                        Binds = [$"{workspaceHostRoot}:/workspace"]
                    }
                },
                cancellationToken);
            await dockerClient.Containers.StartContainerAsync(
                createResponse.ID,
                new ContainerStartParameters(),
                cancellationToken);
            return createResponse.ID;
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

    private static DockerClient CreateDockerClient()
    {
        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrWhiteSpace(dockerHost))
        {
            return new DockerClientConfiguration(new Uri(dockerHost)).CreateClient();
        }

        var endpoint = OperatingSystem.IsWindows()
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";
        return new DockerClientConfiguration(new Uri(endpoint)).CreateClient();
    }
}

internal sealed record DockerExecResult(string Stdout, string Stderr, int ExitCode, bool TimedOut);
