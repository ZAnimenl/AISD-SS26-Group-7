namespace Backend.Services.Grading;

internal sealed class GraderWarmupService : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);
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
        var attempt = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var status = await dockerRuntimeProbe.CheckAsync(stoppingToken);
                if (status.IsAvailable)
                {
                    await graderContainer.EnsureReadyAsync(stoppingToken);
                    logger.LogInformation("Sandbox grader image is warmed up and ready.");
                    return;
                }

                if (attempt == 0)
                {
                    logger.LogInformation("Sandbox grader warmup is waiting for Docker to become reachable.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Sandbox grader warmup failed; retrying in {RetrySeconds} seconds.", RetryDelay.TotalSeconds);
            }

            attempt += 1;
            try
            {
                await Task.Delay(RetryDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
