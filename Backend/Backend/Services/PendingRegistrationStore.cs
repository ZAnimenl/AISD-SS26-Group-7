using System.Collections.Concurrent;

namespace Backend.Services;

internal sealed class PendingRegistrationStore(TimeProvider timeProvider)
{
    private const int GateCount = 64;
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);
    private static readonly StringComparer EmailComparer = StringComparer.OrdinalIgnoreCase;
    private readonly TimeProvider clock = timeProvider;
    private readonly ConcurrentDictionary<string, PendingRegistration> registrations =
        new(EmailComparer);
    private readonly SemaphoreSlim[] gates = Enumerable.Range(0, GateCount)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    internal async ValueTask<PendingRegistrationLease?> AcquireAsync(
        string? email,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var key = NormalizeEmail(email);
        var gate = gates[(int)((uint)EmailComparer.GetHashCode(key) % gates.Length)];
        await gate.WaitAsync(cancellationToken);
        return new PendingRegistrationLease(this, key, gate);
    }

    internal bool IsExpired(PendingRegistration pending)
    {
        return pending.ExpiresAt < clock.GetUtcNow();
    }

    internal async Task CleanExpiredAsync(CancellationToken cancellationToken)
    {
        foreach (var key in registrations.Keys)
        {
            await using var lease = await AcquireAsync(key, cancellationToken);
            if (lease?.Pending is { } pending && IsExpired(pending))
            {
                lease.Remove();
            }
        }
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    internal sealed class PendingRegistrationLease(
        PendingRegistrationStore store,
        string key,
        SemaphoreSlim gate) : IAsyncDisposable
    {
        private int disposed;

        internal PendingRegistration? Pending =>
            store.registrations.TryGetValue(key, out var pending) ? pending : null;

        internal PendingRegistration Restart(
            string fullName,
            string username,
            string email,
            string code)
        {
            var pending = new PendingRegistration(
                fullName,
                username,
                email,
                code,
                store.clock.GetUtcNow().Add(CodeLifetime),
                Attempts: 0);
            store.registrations[key] = pending;
            return pending;
        }

        internal PendingRegistration Refresh(string code)
        {
            var pending = Pending ?? throw new InvalidOperationException("Pending registration no longer exists.");
            var refreshed = pending with
            {
                Code = code,
                ExpiresAt = store.clock.GetUtcNow().Add(CodeLifetime),
                Attempts = 0
            };
            store.registrations[key] = refreshed;
            return refreshed;
        }

        internal void RecordFailedAttempt()
        {
            var pending = Pending ?? throw new InvalidOperationException("Pending registration no longer exists.");
            store.registrations[key] = pending with
            {
                Attempts = pending.Attempts + 1
            };
        }

        internal void Remove()
        {
            store.registrations.TryRemove(key, out _);
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                gate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}

internal sealed record PendingRegistration(
    string FullName,
    string Username,
    string Email,
    string Code,
    DateTimeOffset ExpiresAt,
    int Attempts);
