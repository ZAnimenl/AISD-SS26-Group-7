using Backend.Domain;
using Backend.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>Periodically flips active assessments whose ExpiresAt is in the past to the
/// "expired" status. This keeps the DB state honest so every client (admin table, student
/// list, downstream reports) sees a consistent status without having to recompute it from
/// timestamps on every read.</summary>
public sealed class AssessmentExpirationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AssessmentExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // First tick immediately after startup so a freshly-started server catches up
        // on anything that expired while it was down; subsequent ticks are throttled.
        try
        {
            await ExpireDueAssessmentsAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Initial assessment expiration sweep failed.");
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ExpireDueAssessmentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Assessment expiration worker iteration failed.");
            }
        }
    }

    private async Task ExpireDueAssessmentsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OjSharpDbContext>();
        var now = DateTimeOffset.UtcNow;
        var due = await dbContext.Assessments
            .Where(assessment =>
                assessment.Status == AssessmentStatuses.Active
                && assessment.ExpiresAt != null
                && assessment.ExpiresAt < now)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return;
        }

        foreach (var assessment in due)
        {
            assessment.Status = AssessmentStatuses.Expired;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Marked {Count} assessment(s) as expired.", due.Count);
    }
}
