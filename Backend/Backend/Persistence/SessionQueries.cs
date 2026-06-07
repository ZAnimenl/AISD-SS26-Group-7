using Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

internal static class SessionQueries
{
    public static async Task<AssessmentSession?> FirstUnexpiredAsync(
        IQueryable<AssessmentSession> source,
        DbContext dbContext,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!DatabaseProviders.IsSqliteProviderName(dbContext.Database.ProviderName))
        {
            return await source.FirstOrDefaultAsync(session => session.ExpiresAt > now, cancellationToken);
        }

        var sessions = await source.ToListAsync(cancellationToken);
        return sessions.FirstOrDefault(session => session.ExpiresAt > now);
    }

    public static async Task<List<AssessmentSession>> ToExpiredListAsync(
        IQueryable<AssessmentSession> source,
        DbContext dbContext,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!DatabaseProviders.IsSqliteProviderName(dbContext.Database.ProviderName))
        {
            return await source.Where(session => session.ExpiresAt <= now).ToListAsync(cancellationToken);
        }

        var sessions = await source.ToListAsync(cancellationToken);
        return sessions.Where(session => session.ExpiresAt <= now).ToList();
    }
}
