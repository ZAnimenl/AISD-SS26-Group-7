using System.Diagnostics;
using Backend.Domain;

namespace Backend.Services;

public sealed class CodeEvaluationService
{
    private readonly ICodeRunner codeRunner;

    public CodeEvaluationService(ICodeRunner codeRunner)
    {
        this.codeRunner = codeRunner;
    }

    public async Task<CodeEvaluationResult> EvaluateAsync(
        Guid executionId,
        IEnumerable<TestCase> testCases,
        string code,
        string language,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<TestCaseEvaluationResult>();

        foreach (var testCase in testCases)
        {
            var execution = await codeRunner.RunAsync(code, language, testCase, cancellationToken);
            var output = NormalizeOutput(execution.Stdout);
            var passed = execution.ExitCode == 0;
            var status = BuildTestStatus(execution, passed);

            results.Add(new TestCaseEvaluationResult(
                testCase.Name,
                testCase.Visibility,
                passed,
                output,
                execution.TimedOut ? "Execution timed out." : execution.Stderr,
                status));
        }

        stopwatch.Stop();
        return new CodeEvaluationResult(
            executionId,
            BuildStatus(results),
            BuildStdout(results),
            BuildStderr(results),
            results,
            new ExecutionMetrics(Math.Round(stopwatch.Elapsed.TotalSeconds, 3), 12000));
    }

    public int CalculateScore(int maxScore, IReadOnlyCollection<TestCaseEvaluationResult> testResults)
    {
        if (testResults.Count == 0)
        {
            return 0;
        }

        var passed = testResults.Count(result => result.Passed);
        return (int)Math.Round(maxScore * (double)passed / testResults.Count, MidpointRounding.AwayFromZero);
    }

    public object ToApiObject(CodeEvaluationResult result)
    {
        return new
        {
            execution_id = result.ExecutionId,
            status = result.Status,
            stdout = result.Stdout,
            stderr = result.Stderr,
            test_results = result.TestResults.Select(testResult => new
            {
                testResult.Name,
                testResult.Visibility,
                passed = testResult.Passed,
                output = testResult.Output
            }),
            metrics = new
            {
                cpu_time_seconds = result.Metrics.CpuTimeSeconds,
                peak_memory_kb = result.Metrics.PeakMemoryKb
            }
        };
    }

    private static string BuildStatus(IReadOnlyCollection<TestCaseEvaluationResult> testResults)
    {
        if (testResults.Count > 0 && testResults.All(result => result.Passed))
        {
            return ExecutionStatuses.Passed;
        }

        return testResults.Any(result => result.Status == ExecutionStatuses.RuntimeError)
            ? ExecutionStatuses.RuntimeError
            : ExecutionStatuses.Failed;
    }

    private static string BuildTestStatus(CodeRunResult execution, bool passed)
    {
        if (passed)
        {
            return ExecutionStatuses.Passed;
        }

        return execution.ExitCode == 0 && !execution.TimedOut
            ? ExecutionStatuses.Failed
            : ExecutionStatuses.RuntimeError;
    }

    private static string BuildStdout(IReadOnlyCollection<TestCaseEvaluationResult> testResults)
    {
        var passed = testResults.Count(result => result.Passed);
        return $"{passed}/{testResults.Count} tests passed.";
    }

    private static string? BuildStderr(IReadOnlyCollection<TestCaseEvaluationResult> testResults)
    {
        var failures = testResults.Where(result => !result.Passed).ToList();
        if (failures.Count == 0)
        {
            return null;
        }

        return string.Join(Environment.NewLine, failures.Select(result =>
            string.IsNullOrWhiteSpace(result.Stderr)
                ? $"{result.Name}: {result.Output}"
                : $"{result.Name}: {result.Stderr}"));
    }

    private static string NormalizeOutput(string? output)
    {
        return (output ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    }

}
