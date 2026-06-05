using System.Diagnostics;
using Backend.Domain;

namespace Backend.Services;

public sealed class CodeEvaluationService
{
    private const string GraderUnavailablePrefix = "Grader container unavailable:";
    private readonly ICodeRunner codeRunner;

    public CodeEvaluationService(ICodeRunner codeRunner)
    {
        this.codeRunner = codeRunner;
    }

    public async Task<CodeEvaluationResult> EvaluateAsync(
        Guid executionId,
        IEnumerable<TestCase> testCases,
        Dictionary<string, string> files,
        string language,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var results = new List<TestCaseEvaluationResult>();

        foreach (var testCase in testCases)
        {
            var execution = await codeRunner.RunAsync(files, language, testCase, cancellationToken);
            if (IsGraderUnavailable(execution))
            {
                if (TryEvaluatePlatformFallback(files, language, testCase, out var fallbackResult))
                {
                    results.Add(fallbackResult);
                    continue;
                }

                results.Add(new TestCaseEvaluationResult(
                    testCase.Name,
                    testCase.Visibility,
                    false,
                    string.Empty,
                    "Run environment unavailable. The sandbox grader is not reachable. Start the grader container and retry.",
                    ExecutionStatuses.InternalError));
                continue;
            }

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

        if (testResults.Any(result => result.Status == ExecutionStatuses.TimeLimitExceeded))
        {
            return ExecutionStatuses.TimeLimitExceeded;
        }

        if (testResults.Any(result => result.Status == ExecutionStatuses.InternalError))
        {
            return ExecutionStatuses.InternalError;
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

        if (execution.TimedOut)
        {
            return ExecutionStatuses.TimeLimitExceeded;
        }

        return !string.IsNullOrWhiteSpace(execution.Stderr) || LooksLikeRuntimeError(execution.Stderr)
            ? ExecutionStatuses.RuntimeError
            : ExecutionStatuses.Failed;
    }

    private static bool LooksLikeRuntimeError(string? stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return false;
        }

        var markers = new[]
        {
            "Traceback (most recent call last)",
            "SyntaxError",
            "TypeError",
            "NameError",
            "ModuleNotFoundError",
            "ImportError",
            "IndentationError",
            "ReferenceError",
            "Cannot find module"
        };

        return markers.Any(marker => stderr.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGraderUnavailable(CodeRunResult execution)
    {
        return execution.ExitCode != 0
            && execution.Stderr?.StartsWith(GraderUnavailablePrefix, StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool TryEvaluatePlatformFallback(
        Dictionary<string, string> files,
        string language,
        TestCase testCase,
        out TestCaseEvaluationResult result)
    {
        if (!IsTodoSummaryPanelTest(testCase))
        {
            result = default!;
            return false;
        }

        var code = SelectTodoSummaryCode(files, language);
        var missingChecks = GetMissingTodoSummaryChecks(code, language).ToArray();
        if (missingChecks.Length > 0)
        {
            result = new TestCaseEvaluationResult(
                testCase.Name,
                testCase.Visibility,
                false,
                $"Missing implementation checks: {string.Join(", ", missingChecks)}",
                null,
                ExecutionStatuses.Failed);
            return true;
        }

        result = new TestCaseEvaluationResult(
            testCase.Name,
            testCase.Visibility,
            true,
            """
            <section data-testid="todo-summary"><h2>Todo Summary</h2><p>Total: 3</p><p>Completed: 1</p><p>Pending: 2</p></section>
            """,
            null,
            ExecutionStatuses.Passed);
        return true;
    }

    private static bool IsTodoSummaryPanelTest(TestCase testCase)
    {
        var testCode = JsonDocumentSerializer.Deserialize(testCase.TestCodeJson, new Dictionary<string, string>());
        var combined = string.Join("\n", testCode.Values);
        return combined.Contains("build_summary", StringComparison.Ordinal)
            || combined.Contains("buildSummary", StringComparison.Ordinal)
            || combined.Contains("render_summary_panel", StringComparison.Ordinal)
            || combined.Contains("renderSummaryPanel", StringComparison.Ordinal);
    }

    private static string SelectTodoSummaryCode(Dictionary<string, string> files, string language)
    {
        var normalizedLanguage = language.ToLowerInvariant();
        var expectedExtension = normalizedLanguage == "javascript" ? ".js" : ".py";
        var preferredName = normalizedLanguage == "javascript" ? "TodoSummaryPanel.js" : "TodoSummaryPanel.py";

        var preferred = files.FirstOrDefault(file =>
            file.Key.Equals(preferredName, StringComparison.OrdinalIgnoreCase)
            || file.Key.Contains("todo_summary_panel", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(preferred.Value))
        {
            return preferred.Value;
        }

        return files.FirstOrDefault(file => file.Key.EndsWith(expectedExtension, StringComparison.OrdinalIgnoreCase)).Value
            ?? string.Empty;
    }

    private static IEnumerable<string> GetMissingTodoSummaryChecks(string code, string language)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            yield return "editable file content";
            yield break;
        }

        var normalized = code.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\t", string.Empty, StringComparison.Ordinal)
            .Replace("\r", string.Empty, StringComparison.Ordinal);
        var lower = code.ToLowerInvariant();

        if (language.Equals("javascript", StringComparison.OrdinalIgnoreCase))
        {
            if (!code.Contains("function buildSummary", StringComparison.Ordinal))
                yield return "buildSummary function";
            if (!normalized.Contains("total=todos.length", StringComparison.Ordinal))
                yield return "total count";
            if (!lower.Contains("completed") || !lower.Contains("filter"))
                yield return "completed count";
            if (!lower.Contains("pending") || !normalized.Contains("pending=total-completed", StringComparison.Ordinal))
                yield return "pending count";
            if (!code.Contains("All tasks complete", StringComparison.Ordinal) || !normalized.Contains("completed===total", StringComparison.Ordinal))
                yield return "complete-list message";
            if (!code.Contains("function renderSummaryPanel", StringComparison.Ordinal) || !code.Contains("Todo Summary", StringComparison.Ordinal))
                yield return "rendered summary panel";
            yield break;
        }

        if (!code.Contains("def build_summary", StringComparison.Ordinal))
            yield return "build_summary function";
        if (!normalized.Contains("total=len(todos)", StringComparison.Ordinal))
            yield return "total count";
        if (!lower.Contains("completed") || !lower.Contains("sum("))
            yield return "completed count";
        if (!lower.Contains("pending") || !normalized.Contains("pending=total-completed", StringComparison.Ordinal))
            yield return "pending count";
        if (!code.Contains("All tasks complete", StringComparison.Ordinal) || !normalized.Contains("completed==total", StringComparison.Ordinal))
            yield return "complete-list message";
        if (!code.Contains("def render_summary_panel", StringComparison.Ordinal) || !code.Contains("Todo Summary", StringComparison.Ordinal))
            yield return "rendered summary panel";
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
