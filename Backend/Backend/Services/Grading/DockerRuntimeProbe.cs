namespace Backend.Services.Grading;

internal sealed class DockerRuntimeProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(1.5);

    public async Task<DockerRuntimeStatus> CheckAsync(CancellationToken cancellationToken)
    {
        var endpoint = DockerGraderContainer.ResolveDockerEndpoint(
            Environment.GetEnvironmentVariable("DOCKER_HOST"),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            OperatingSystem.IsWindows(),
            Path.Exists);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(ProbeTimeout);

        try
        {
            using var dockerClient = DockerGraderContainer.CreateDockerClient(endpoint);
            await dockerClient.System.PingAsync(timeoutSource.Token);
            return new DockerRuntimeStatus(true);
        }
        catch
        {
            return new DockerRuntimeStatus(false);
        }
    }
}

internal sealed record DockerRuntimeStatus(bool IsAvailable);
