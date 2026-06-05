using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public static class DatabaseAdvisoryLocks
{
    public const long SchemaCompatibility = 744733859200000001L;

    public static async Task<IAsyncDisposable> AcquireSessionLockAsync(
        OjSharpDbContext dbContext,
        long lockKey,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_lock({lockKey})",
                cancellationToken);
            return new SessionLockHandle(dbContext, lockKey);
        }
        catch
        {
            await dbContext.Database.CloseConnectionAsync();
            throw;
        }
    }

    private sealed class SessionLockHandle(OjSharpDbContext dbContext, long lockKey) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_unlock({lockKey})");
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }
    }
}
