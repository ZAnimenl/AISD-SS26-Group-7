using Backend.Domain;
using Backend.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

/// <summary>Periodically closes active assessments whose ExpiresAt is in the past.
/// This keeps persisted assessment status consistent for every client without requiring
/// each read path to derive the status from timestamps.</summary>
public sealed class AssessmentClosingWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AssessmentClosingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await CloseDueAssessmentsAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Initial assessment closing sweep failed.");
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CloseDueAssessmentsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Assessment closing worker iteration failed.");
            }
        }
    }

    private async Task CloseDueAssessmentsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OjSharpDbContext>();
        var now = DateTimeOffset.UtcNow;
        // SQLite cannot translate DateTimeOffset ordering. Keep the database-side
        // status/null filters, then compare the small scheduled set in memory.
        var scheduled = await dbContext.Assessments
            .Where(assessment =>
                assessment.Status == AssessmentStatuses.Active
                && assessment.ExpiresAt != null)
            .ToListAsync(cancellationToken);
        var due = scheduled
            .Where(assessment => assessment.ExpiresAt < now)
            .Take(50)
            .ToList();

        if (due.Count == 0)
        {
            return;
        }

        foreach (var assessment in due)
        {
            assessment.Status = AssessmentStatuses.Closed;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Closed {Count} assessment(s) after their deadline.", due.Count);
    }
}
