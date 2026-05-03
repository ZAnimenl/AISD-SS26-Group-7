using System.Text;
using Backend.Domain;
using Backend.Services;
using Backend.Services.Grading;

namespace OjSharp.Tests.ApiContractTests;

public sealed class CodeEvaluationServiceTests
{
    [Fact]
    public async Task Evaluation_runs_all_test_cases_before_scoring()
    {
        var runner = new RecordingCodeRunner([
            new CodeRunResult("1", null, 0, false),
            new CodeRunResult("wrong", null, 0, false),
            new CodeRunResult(string.Empty, "boom", 1, false)
        ]);
        var service = new CodeEvaluationService(runner);
        var testCases = new[]
        {
            TestCase("first", "input-1", "1"),
            TestCase("second", "input-2", "2"),
            TestCase("third", "input-3", "3")
        };

        var result = await service.EvaluateAsync(Guid.NewGuid(), testCases, "code", "python", CancellationToken.None);

        Assert.Equal(3, runner.TestCaseNames.Count);
        Assert.Equal(["first", "second", "third"], runner.TestCaseNames);
        Assert.Equal(ExecutionStatuses.RuntimeError, result.Status);
        Assert.Equal(2, result.TestResults.Count(testResult => testResult.Passed));
        Assert.Equal(67, service.CalculateScore(100, result.TestResults));
    }

    [Fact]
    public async Task Python_solution_is_evaluated_against_test_case_output()
    {
        var runner = new RecordingCodeRunner([
            new CodeRunResult("6", null, 0, false),
            new CodeRunResult("12", null, 0, false)
        ]);
        var service = new CodeEvaluationService(runner);
        var testCases = new[]
        {
            TestCase("public", "[1,2,3]", "6"),
            TestCase("hidden", "[-3,5,10]", "12")
        };

        var result = await service.EvaluateAsync(
            Guid.NewGuid(),
            testCases,
            "def solve(arr):\n    return sum(arr)\n",
            "python",
            CancellationToken.None);

        Assert.Equal(ExecutionStatuses.Passed, result.Status);
        Assert.All(result.TestResults, testResult => Assert.True(testResult.Passed));
        Assert.Equal(50, service.CalculateScore(50, result.TestResults));
    }

    [Fact]
    public void Score_is_based_on_passed_test_case_ratio()
    {
        var service = new CodeEvaluationService(new RecordingCodeRunner([]));
        var results = new[]
        {
            TestResult(true),
            TestResult(false),
            TestResult(true),
            TestResult(false)
        };

        var score = service.CalculateScore(50, results);

        Assert.Equal(25, score);
    }

    [Fact]
    public void Grader_commands_use_pytest_jest_and_typescript_compile_step()
    {
        var factory = new GraderCommandFactory();

        var pythonCommand = factory.Create(GradingLanguage.Python);
        var javascriptCommand = factory.Create(GradingLanguage.JavaScript);
        var typeScriptCommand = factory.Create(GradingLanguage.TypeScript);

        Assert.Contains("pytest", pythonCommand);
        Assert.Contains("no:cacheprovider", pythonCommand);
        Assert.Contains("jest", javascriptCommand);
        Assert.Contains("tsc solution.ts", string.Join(" ", typeScriptCommand));
        Assert.Contains("jest", string.Join(" ", typeScriptCommand));
    }

    [Fact]
    public void Grader_test_files_store_admin_test_code_for_python_javascript_and_typescript()
    {
        var factory = new GradingTestFileFactory();
        var pythonDirectory = Directory.CreateTempSubdirectory("ojsharp-python-test-");
        var javascriptDirectory = Directory.CreateTempSubdirectory("ojsharp-javascript-test-");
        var typeScriptDirectory = Directory.CreateTempSubdirectory("ojsharp-typescript-test-");
        try
        {
            factory.Write(pythonDirectory.FullName, "def solve(value):\n    return value\n", "from solution import solve\n", GradingLanguage.Python);
            factory.Write(javascriptDirectory.FullName, "function solve(value) {\n  return value;\n}\n", "const { solve } = require(\"./solution.js\");\n", GradingLanguage.JavaScript);
            factory.Write(typeScriptDirectory.FullName, "function solve(value: string): string {\n  return value;\n}\n", "const solve = globalThis.__ojsharpSolve;\n", GradingLanguage.TypeScript);

            Assert.Contains("from solution import solve", File.ReadAllText(Path.Combine(pythonDirectory.FullName, "test_solution.py")));
            Assert.Contains("require(\"./solution.js\")", File.ReadAllText(Path.Combine(javascriptDirectory.FullName, "solution.test.js")));
            Assert.Contains("__ojsharpSolve", File.ReadAllText(Path.Combine(typeScriptDirectory.FullName, "solution.ts")));
            Assert.Contains("__ojsharpSolve", File.ReadAllText(Path.Combine(typeScriptDirectory.FullName, "solution.test.js")));
        }
        finally
        {
            pythonDirectory.Delete(true);
            javascriptDirectory.Delete(true);
            typeScriptDirectory.Delete(true);
        }
    }

    [Fact]
    public async Task Docker_runner_rejects_unsupported_language_without_starting_container()
    {
        var runner = new DockerCodeRunner();

        var result = await runner.RunAsync(
            "public class Solution {}",
            "csharp",
            TestCase("unsupported", "1", "1"),
            CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unsupported language", result.Stderr);
    }

    [Fact]
    public void Docker_build_context_contains_grader_dockerfile()
    {
        using var context = DockerBuildContextFactory.Create();
        var header = new byte[512];

        Assert.Equal(512, context.Read(header));
        var headerText = Encoding.ASCII.GetString(header);
        using var reader = new StreamReader(context, Encoding.UTF8);
        var bodyText = reader.ReadToEnd();

        Assert.Contains("Dockerfile", headerText);
        Assert.Contains("pytest", bodyText);
        Assert.Contains("jest", bodyText);
        Assert.Contains("typescript", bodyText);
        Assert.Contains("PYTHONDONTWRITEBYTECODE", bodyText);
    }

    private static TestCase TestCase(string name, string input, string expectedOutput)
    {
        return new TestCase
        {
            Id = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            Name = name,
            Visibility = TestCaseVisibilities.Public,
            TestCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["python"] = $"def test_{name.Replace("-", "_")}():\n    assert True\n",
                ["javascript"] = $"test(\"{name}\", () => expect(true).toBe(true));\n",
                ["typescript"] = $"test(\"{name}\", () => expect(true).toBe(true));\n"
            })
        };
    }

    private static TestCaseEvaluationResult TestResult(bool passed)
    {
        return new TestCaseEvaluationResult(
            "test",
            TestCaseVisibilities.Public,
            passed,
            string.Empty,
            null,
            passed ? ExecutionStatuses.Passed : ExecutionStatuses.Failed);
    }

    private sealed class RecordingCodeRunner(IReadOnlyList<CodeRunResult> results) : ICodeRunner
    {
        private int index;

        public List<string> TestCaseNames { get; } = [];

        public Task<CodeRunResult> RunAsync(string code, string language, TestCase testCase, CancellationToken cancellationToken)
        {
            TestCaseNames.Add(testCase.Name);
            return Task.FromResult(results[index++]);
        }
    }
}
