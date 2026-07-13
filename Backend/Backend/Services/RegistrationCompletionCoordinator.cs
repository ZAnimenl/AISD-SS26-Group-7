namespace Backend.Services;

internal sealed class RegistrationCompletionCoordinator
{
    private readonly SemaphoreSlim gate = new(1, 1);

    internal async Task<T> RunAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            gate.Release();
        }
    }
}
