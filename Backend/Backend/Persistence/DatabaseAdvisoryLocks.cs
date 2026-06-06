using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public static class DatabaseAdvisoryLocks
{
    private static readonly ConcurrentDictionary<long, SemaphoreSlim> LocalLocks = new();

    public const long SchemaCompatibility = 744733859200000001L;

    public static long GetAssessmentAttemptKey(Guid assessmentId, Guid userId)
    {
        var input = new byte[33];
        input[0] = 2;
        _ = assessmentId.TryWriteBytes(input.AsSpan(1, 16));
        _ = userId.TryWriteBytes(input.AsSpan(17, 16));

        var hash = SHA256.HashData(input);
        return BinaryPrimitives.ReadInt64LittleEndian(hash);
    }

    public static async Task<IAsyncDisposable> AcquireSessionLockAsync(
        OjSharpDbContext dbContext,
        long lockKey,
        CancellationToken cancellationToken)
    {
        if (DatabaseProviders.IsSqliteProviderName(dbContext.Database.ProviderName))
        {
            var localLock = LocalLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
            await localLock.WaitAsync(cancellationToken);
            return new LocalLockHandle(localLock);
        }

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

    public static async Task AcquireTransactionLockAsync(
        OjSharpDbContext dbContext,
        long lockKey,
        CancellationToken cancellationToken)
    {
        if (DatabaseProviders.IsSqliteProviderName(dbContext.Database.ProviderName))
        {
            return;
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})",
            cancellationToken);
    }

    private sealed class LocalLockHandle(SemaphoreSlim localLock) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            localLock.Release();
            return ValueTask.CompletedTask;
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
