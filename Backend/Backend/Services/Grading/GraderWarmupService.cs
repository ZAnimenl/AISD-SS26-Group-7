namespace Backend.Services.Grading;

internal sealed class GraderWarmupService : BackgroundService
{
    private readonly DockerRuntimeProbe dockerRuntimeProbe;
    private readonly DockerGraderContainer graderContainer;
    private readonly ILogger<GraderWarmupService> logger;

    public GraderWarmupService(
        DockerRuntimeProbe dockerRuntimeProbe,
        DockerGraderContainer graderContainer,
        ILogger<GraderWarmupService> logger)
    {
        this.dockerRuntimeProbe = dockerRuntimeProbe;
        this.graderContainer = graderContainer;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var status = await dockerRuntimeProbe.CheckAsync(stoppingToken);
            if (!status.IsAvailable)
            {
                logger.LogInformation("Skipping grader warmup because Docker is not reachable.");
                return;
            }

            await graderContainer.EnsureReadyAsync(stoppingToken);
            logger.LogInformation("Sandbox grader is warmed up and ready.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Sandbox grader warmup failed; the first run will retry on demand.");
        }
    }
}
