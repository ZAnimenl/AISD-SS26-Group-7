using Backend;
using Microsoft.Extensions.Logging;

namespace OjSharp.Tests.ApiContractTests;

public sealed class StartupDiagnosticsTests
{
    [Fact]
    public void Database_initialization_logging_does_not_throw_when_logger_fails()
    {
        var logger = new ThrowingLogger();
        var databaseException = new InvalidOperationException("Database connection failed.");

        var exception = Record.Exception(() =>
            StartupDiagnostics.LogDatabaseInitializationFailure(logger, databaseException));

        Assert.Null(exception);
    }

    private sealed class ThrowingLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            throw new InvalidOperationException("Logger sink failed.");
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
