using Backend.Domain;

namespace Backend.Services.Grading;

internal sealed class DockerCodeRunner : ICodeRunner
{
    private static readonly TimeSpan HostTimeout = TimeSpan.FromSeconds(10);
    private readonly DockerGraderContainer graderContainer;
    private readonly GradingWorkspace workspace;
    private readonly GradingTestFileFactory testFileFactory;
    private readonly GraderCommandFactory commandFactory;

    public DockerCodeRunner()
        : this(
            new DockerGraderContainer(),
            new GradingWorkspace(),
            new GradingTestFileFactory(),
            new GraderCommandFactory())
    {
    }

    public DockerCodeRunner(
        DockerGraderContainer graderContainer,
        GradingWorkspace workspace,
        GradingTestFileFactory testFileFactory,
        GraderCommandFactory commandFactory)
    {
        this.graderContainer = graderContainer;
        this.workspace = workspace;
        this.testFileFactory = testFileFactory;
        this.commandFactory = commandFactory;
    }

    public async Task<CodeRunResult> RunAsync(
        string code,
        string language,
        TestCase testCase,
        CancellationToken cancellationToken)
    {
        if (!GradingLanguageParser.TryParse(language, out var gradingLanguage))
        {
            return new CodeRunResult(string.Empty, $"Unsupported language: {language}", 1, false);
        }

        var testCode = SelectTestCode(testCase, language);
        if (string.IsNullOrWhiteSpace(testCode))
        {
            return new CodeRunResult(string.Empty, $"No test code is configured for language: {language}", 1, false);
        }

        try
        {
            using var run = workspace.CreateRun();
            testFileFactory.Write(run.HostPath, code, testCode, gradingLanguage);

            var execution = await graderContainer.ExecuteAsync(
                run.ContainerPath,
                commandFactory.Create(gradingLanguage),
                HostTimeout,
                cancellationToken);
            var output = workspace.ReadActualOutput(run) ?? execution.Stdout;

            return new CodeRunResult(
                output,
                execution.ExitCode == 0 ? null : BuildStderr(execution),
                execution.ExitCode,
                execution.TimedOut);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new CodeRunResult(string.Empty, $"Grader container unavailable: {exception.Message}", 1, false);
        }
    }

    private static string SelectTestCode(TestCase testCase, string language)
    {
        var testCode = JsonDocumentSerializer.Deserialize(testCase.TestCodeJson, new Dictionary<string, string>());
        return testCode.GetValueOrDefault(language)
               ?? testCode.GetValueOrDefault(language.ToLowerInvariant())
               ?? string.Empty;
    }

    private static string BuildStderr(DockerExecResult execution)
    {
        if (execution.TimedOut)
        {
            return "Execution timed out.";
        }

        return string.Join(
            Environment.NewLine,
            new[] { execution.Stdout, execution.Stderr }.Where(value => !string.IsNullOrWhiteSpace(value)));
    }
}
