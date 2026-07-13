using Backend.Domain;

namespace Backend.Services.Grading;

internal sealed class DockerCodeRunner : ICodeRunner
{
    private static readonly TimeSpan HostTimeout = TimeSpan.FromMilliseconds(9500);
    private static readonly TimeSpan BrowserPreviewHostTimeout = TimeSpan.FromSeconds(5);
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
        Dictionary<string, string> files,
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
            var previewEntry = GetBrowserPreviewEntry(testCase, language);
            if (previewEntry is not null)
            {
                testFileFactory.WriteBrowserPreview(run.HostPath, files);
            }
            else
            {
                testFileFactory.Write(
                    run.HostPath,
                    files,
                    testCode,
                    gradingLanguage,
                    isHtmlWorkspace: language.Equals("html", StringComparison.OrdinalIgnoreCase));
            }

            var execution = await graderContainer.ExecuteAsync(
                run.HostPath,
                previewEntry is null
                    ? commandFactory.Create(gradingLanguage)
                    : commandFactory.CreateBrowserPreview(previewEntry),
                previewEntry is null ? HostTimeout : BrowserPreviewHostTimeout,
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
            return new CodeRunResult(string.Empty, "Grader container unavailable: the run request was canceled before the sandbox became ready.", 1, false);
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
               ?? (language.Equals("html", StringComparison.OrdinalIgnoreCase)
                   ? testCode.GetValueOrDefault("javascript")
                   : null)
               ?? (language.Equals("javascript", StringComparison.OrdinalIgnoreCase)
                   ? testCode.GetValueOrDefault("html")
                   : null)
               ?? string.Empty;
    }

    private static string? GetBrowserPreviewEntry(TestCase testCase, string language)
    {
        if (!language.Equals("html", StringComparison.OrdinalIgnoreCase)
            && !language.Equals("javascript", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var adminMetadata = JsonDocumentSerializer.Deserialize(
            testCase.AdminMetadataJson,
            new Dictionary<string, string>());
        if (!string.Equals(
                adminMetadata.GetValueOrDefault("source"),
                "browser_ui_preview_run",
                StringComparison.Ordinal)
            || !string.Equals(
                adminMetadata.GetValueOrDefault("synthetic"),
                "true",
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                adminMetadata.GetValueOrDefault("execution_profile"),
                "browser_preview_packager",
                StringComparison.Ordinal))
        {
            return null;
        }

        var publicMetadata = JsonDocumentSerializer.Deserialize(
            testCase.PublicMetadataJson,
            new Dictionary<string, string>());
        var previewEntry = publicMetadata.GetValueOrDefault("preview_entry");
        if (string.IsNullOrWhiteSpace(previewEntry)
            || !previewEntry.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(previewEntry).Equals(previewEntry, StringComparison.Ordinal))
        {
            return null;
        }

        return previewEntry;
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
