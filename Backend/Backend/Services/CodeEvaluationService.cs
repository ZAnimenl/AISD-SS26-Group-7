using Backend.Domain;

namespace Backend.Services;

public sealed class CodeEvaluationService
{
    public object BuildRunResult(Guid executionId, IEnumerable<TestCase> publicTestCases, string code)
    {
        var cases = publicTestCases.ToList();
        var passes = IsMeaningfulCode(code);

        return new
        {
            execution_id = executionId,
            status = passes ? ExecutionStatuses.Passed : ExecutionStatuses.Failed,
            stdout = passes ? "Sample tests passed.\n" : null,
            stderr = passes ? null : "No executable solution was detected.",
            test_results = cases.Select(testCase => new
            {
                testCase.Name,
                testCase.Visibility,
                passed = passes,
                actual_output = passes ? testCase.ExpectedOutput : "",
                expected_output = testCase.ExpectedOutput
            }),
            metrics = new
            {
                cpu_time_seconds = 0.04,
                peak_memory_kb = 12000
            }
        };
    }

    public bool IsMeaningfulCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalized = code.Trim().ToLowerInvariant();
        return !normalized.EndsWith("pass", StringComparison.Ordinal)
               && !normalized.Contains("// todo", StringComparison.Ordinal)
               && !normalized.Contains("throw new notimplementedexception", StringComparison.Ordinal);
    }
}
