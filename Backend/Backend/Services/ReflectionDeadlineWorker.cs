using Backend.Domain;
using Backend.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public sealed class ReflectionDeadlineWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ReflectionDeadlineWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (!stoppingToken.IsCancellationRequested
               && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await FinalizeExpiredReflectionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Reflection deadline worker iteration failed.");
            }
        }
    }

    private async Task FinalizeExpiredReflectionsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OjSharpDbContext>();
        var gradingService = scope.ServiceProvider.GetRequiredService<AiUsageGradingService>();
        var expired = await dbContext.AssessmentSessions
            .Where(item =>
                item.AiGradingStatus == AiGradingStatuses.ReflectionPending
                && item.ReflectionSubmittedAt == null
                && item.ReflectionDeadline != null)
            .ToListAsync(cancellationToken);
        expired = expired
            .Where(item => item.ReflectionDeadline <= DateTimeOffset.UtcNow)
            .Take(10)
            .ToList();

        foreach (var session in expired)
        {
            session.ReflectionSubmittedAt = session.ReflectionDeadline;
            session.ReflectionSubmissionReason = "timeout";
            session.ReflectionWordCount = AiUsageGradingService.CountWords(session.ReflectionText);
            await dbContext.SaveChangesAsync(cancellationToken);
            await gradingService.GradeAsync(session, cancellationToken);
        }
    }
}
