using Backend.Domain;

namespace Backend.Services;

public interface ICodeRunner
{
    Task<CodeRunResult> RunAsync(string code, string language, TestCase testCase, CancellationToken cancellationToken);
}

public sealed record CodeEvaluationResult(
    Guid ExecutionId,
    string Status,
    string Stdout,
    string? Stderr,
    IReadOnlyList<TestCaseEvaluationResult> TestResults,
    ExecutionMetrics Metrics);

public sealed record TestCaseEvaluationResult(
    string Name,
    string Visibility,
    bool Passed,
    string Output,
    string? Stderr,
    string Status);

public sealed record ExecutionMetrics(double CpuTimeSeconds, int PeakMemoryKb);

public sealed record CodeRunResult(string Stdout, string? Stderr, int ExitCode, bool TimedOut);
