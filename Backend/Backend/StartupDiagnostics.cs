using Microsoft.Extensions.Logging;

namespace Backend;

public static class StartupDiagnostics
{
    private const string DatabaseInitializationFailureMessage =
        "Database initialization failed. Verify the configured PostgreSQL connection and seed administrator settings.";

    public static void LogDatabaseInitializationFailure(ILogger logger, Exception exception)
    {
        try
        {
            logger.LogWarning(exception, DatabaseInitializationFailureMessage);
        }
        catch (Exception loggingException)
        {
            WriteFallbackDatabaseInitializationFailure(exception, loggingException);
        }
    }

    private static void WriteFallbackDatabaseInitializationFailure(Exception exception, Exception loggingException)
    {
        try
        {
            Console.Error.WriteLine(DatabaseInitializationFailureMessage);
            Console.Error.WriteLine($"Original database initialization error: {exception.GetType().FullName}: {exception.Message}");
            Console.Error.WriteLine($"Startup logging failed: {loggingException.GetType().FullName}: {loggingException.Message}");
        }
        catch
        {
            // Startup must continue even if the fallback diagnostic stream is unavailable.
        }
    }
}
