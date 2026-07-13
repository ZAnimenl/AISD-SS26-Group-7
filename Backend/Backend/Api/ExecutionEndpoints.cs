using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api;

public static class ExecutionEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/assessments/{assessmentId:guid}/questions/{questionId:guid}/run", RunByAssessmentAsync);
        api.MapGet("/executions/{executionId:guid}", GetAsync);
    }

    private static async Task<IResult> RunByAssessmentAsync(
        Guid assessmentId,
        Guid questionId,
        AssessmentRunCodeRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SessionClock sessionClock,
        CodeEvaluationService evaluationService,
        CancellationToken cancellationToken)
    {
        var (user, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Student, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var session = await SessionQueries.FirstUnexpiredAsync(
            dbContext.AssessmentSessions
                .Include(item => item.Assessment)
                .Where(item => item.AssessmentId == assessmentId
                               && item.UserId == user!.Id
                               && item.Status == SessionStatuses.Active),
            dbContext,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("ATTEMPT_NOT_FOUND", "Active assessment attempt was not found.", StatusCodes.Status404NotFound);
        }

        if (!AssessmentPolicy.IsAssessmentActive(session.Assessment))
        {
            return ApiResults.Error("ASSESSMENT_CLOSED", "This assessment is not accepting new runs.", StatusCodes.Status409Conflict);
        }

        if (sessionClock.IsClosed(session))
        {
            return ApiResults.Error("ATTEMPT_EXPIRED", "The assessment attempt has expired.", StatusCodes.Status409Conflict);
        }

        return await RunForSessionAsync(
            session.Id,
            session.AssessmentId,
            questionId,
            request.SelectedLanguage,
            request.Files,
            dbContext,
            evaluationService,
            cancellationToken);
    }

    private static async Task<IResult> RunForSessionAsync(
        Guid sessionId,
        Guid assessmentId,
        Guid questionId,
        string selectedLanguage,
        Dictionary<string, string> files,
        OjSharpDbContext dbContext,
        CodeEvaluationService evaluationService,
        CancellationToken cancellationToken)
    {
        var question = await dbContext.Questions.FirstOrDefaultAsync(
            question => question.Id == questionId && question.AssessmentId == assessmentId,
            cancellationToken);
        if (question is null)
        {
            return ApiResults.Error("QUESTION_NOT_FOUND", "Question was not found for this assessment.", StatusCodes.Status404NotFound);
        }

        var normalizedLanguage = AssessmentPolicy.NormalizeLanguage(selectedLanguage);
        if (!AssessmentPolicy.IsStudentLanguageAllowed(question, normalizedLanguage))
        {
            return ApiResults.Error(
                "LANGUAGE_NOT_ALLOWED",
                "The selected language is not allowed for this task.",
                StatusCodes.Status400BadRequest);
        }

        var publicTests = await dbContext.TestCases
            .Where(testCase => testCase.QuestionId == questionId && testCase.Visibility == TestCaseVisibilities.Public)
            .ToListAsync(cancellationToken);
        if (question.VerificationMode == VerificationModes.BrowserUiPreview)
        {
            publicTests.Insert(0, CreateBrowserPreviewTest(question, normalizedLanguage));
        }

        var executionId = Guid.NewGuid();
        var result = await evaluationService.EvaluateAsync(
            executionId,
            publicTests,
            files,
            normalizedLanguage,
            cancellationToken);
        if (question.VerificationMode == VerificationModes.BrowserUiPreview)
        {
            var previewResult = result.TestResults.FirstOrDefault(item =>
                item.Name == "Browser preview render" && item.Passed);
            result = result with
            {
                PreviewDocument = previewResult is null
                    ? null
                    : LimitPreviewDocument(previewResult.Output)
            };
        }

        dbContext.ExecutionRecords.Add(new ExecutionRecord
        {
            Id = executionId,
            SessionId = sessionId,
            QuestionId = questionId,
            Status = result.Status,
            Stdout = result.Stdout,
            Stderr = result.Stderr,
            TestResultsJson = JsonDocumentSerializer.Serialize(result.TestResults.Select(testResult => new
            {
                testResult.Name,
                testResult.Visibility,
                passed = testResult.Passed,
                output = testResult.Output
            })),
            MetricsJson = JsonDocumentSerializer.Serialize(new
            {
                cpu_time_seconds = result.Metrics.CpuTimeSeconds,
                peak_memory_kb = result.Metrics.PeakMemoryKb
            }),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResults.Success(evaluationService.ToApiObject(result));
    }

    private static async Task<IResult> GetAsync(
        Guid executionId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (user, error) = await currentUserAccessor.RequireUserAsync(httpContext, dbContext, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var record = await dbContext.ExecutionRecords
            .FirstOrDefaultAsync(item => item.Id == executionId, cancellationToken);
        if (record is null)
        {
            return ApiResults.Error("NOT_FOUND", "Execution was not found.", StatusCodes.Status404NotFound);
        }

        if (user!.Role == UserRoles.Student)
        {
            var ownerUserId = await dbContext.AssessmentSessions
                .Where(session => session.Id == record.SessionId)
                .Select(session => (Guid?)session.UserId)
                .FirstOrDefaultAsync(cancellationToken);
            if (ownerUserId != user.Id)
            {
                return ApiResults.Error("FORBIDDEN", "The current user cannot access this execution.", StatusCodes.Status403Forbidden);
            }
        }

        if (user.Role != UserRoles.Student && user.Role != UserRoles.Administrator)
        {
            return ApiResults.Error("FORBIDDEN", "The current user cannot access this execution.", StatusCodes.Status403Forbidden);
        }

        return ApiResults.Success(new
        {
            execution_id = record.Id,
            record.Status,
            stdout = record.Stdout,
            stderr = record.Stderr,
            test_results = JsonDocumentSerializer.Deserialize(record.TestResultsJson, Array.Empty<object>()),
            metrics = JsonDocumentSerializer.Deserialize(record.MetricsJson, new Dictionary<string, object>())
        });
    }

    private static TestCase CreateBrowserPreviewTest(Question question, string selectedLanguage)
    {
        var metadata = JsonDocumentSerializer.Deserialize(question.VerificationMetadataJson, new Dictionary<string, string>());
        var normalizedLanguage = AssessmentPolicy.NormalizeLanguage(selectedLanguage);
        var starterCode = JsonDocumentSerializer.DeserializeStarterCode(question.StarterCodeJson);
        var languageStarterFiles = GetStarterFilesForLanguage(starterCode, normalizedLanguage);
        var configuredPreviewEntry = metadata.GetValueOrDefault("preview_entry");
        var usesJavaScriptPreviewHarness = normalizedLanguage is "javascript" or "html";
        var htmlPreviewEntry = usesJavaScriptPreviewHarness
            ? GetJavaScriptHtmlPreviewEntry(languageStarterFiles, configuredPreviewEntry)
            : null;
        var defaultEntry = usesJavaScriptPreviewHarness
            ? "TodoSummaryPanel.js"
            : "TodoSummaryPanel.py";
        var languageStarterEntry = starterCode
            .GetValueOrDefault(normalizedLanguage, new Dictionary<string, string>())
            .Keys
            .FirstOrDefault(fileName => IsPreviewEntryForLanguage(fileName, normalizedLanguage));
        var previewEntry = IsPreviewEntryForLanguage(configuredPreviewEntry, normalizedLanguage)
            ? configuredPreviewEntry!
            : languageStarterEntry ?? defaultEntry;
        var pythonModule = GetSafePythonModuleName(previewEntry, "TodoSummaryPanel");
        var javascriptModule = GetSafeJavaScriptModulePath(previewEntry, "TodoSummaryPanel.js");
        var javascriptTestCode = htmlPreviewEntry is not null
            ? CreateHtmlPreviewTestCode(htmlPreviewEntry)
            : $$"""
            const fs = require('fs');
            const { renderSummaryPanel } = require('./{{javascriptModule}}');

            test('browser preview render', () => {
              const todos = [
                { title: 'Buy groceries', completed: false },
                { title: 'Write tests', completed: true },
                { title: 'Review PR', completed: false },
              ];
              const html = String(renderSummaryPanel(todos));
              fs.writeFileSync('actual.txt', html, 'utf8');
              expect(html).toContain('Todo Summary');
              expect(html).toContain('<');
            });
            """;
        var publicMetadata = new Dictionary<string, string>
        {
            ["student_visible"] = "true",
            ["preview_output"] = "html"
        };
        if (htmlPreviewEntry is not null)
        {
            publicMetadata["preview_entry"] = htmlPreviewEntry;
        }

        return new TestCase
        {
            Id = Guid.NewGuid(),
            QuestionId = question.Id,
            Name = "Browser preview render",
            Visibility = TestCaseVisibilities.Public,
            TestCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["python"] = $$"""
                from pathlib import Path
                from {{pythonModule}} import render_summary_panel

                def test_browser_preview_render():
                    todos = [
                        {"title": "Buy groceries", "completed": False},
                        {"title": "Write tests", "completed": True},
                        {"title": "Review PR", "completed": False},
                    ]
                    html = str(render_summary_panel(todos))
                    Path("actual.txt").write_text(html, encoding="utf-8")
                    assert "Todo Summary" in html
                    assert "<" in html and ">" in html
                """,
                ["javascript"] = javascriptTestCode,
                ["html"] = javascriptTestCode
            }),
            AuthoringSource = question.AuthoringSource,
            PublicMetadataJson = JsonDocumentSerializer.Serialize(publicMetadata),
            AdminMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["synthetic"] = "true",
                ["source"] = "browser_ui_preview_run",
                ["execution_profile"] = "browser_preview_packager"
            }),
            TraceabilityMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["requirements"] = "REQ-18e,REQ-30c"
            })
        };
    }

    private static string? GetJavaScriptHtmlPreviewEntry(
        IReadOnlyDictionary<string, string> languageStarterFiles,
        string? configuredPreviewEntry)
    {
        var configuredFileName = IsSafeHtmlFile(configuredPreviewEntry)
            ? Path.GetFileName(configuredPreviewEntry)
            : null;
        if (configuredFileName is not null && languageStarterFiles.ContainsKey(configuredFileName))
        {
            return configuredFileName;
        }

        return languageStarterFiles.Keys.FirstOrDefault(IsSafeHtmlFile);
    }

    private static string CreateHtmlPreviewTestCode(string htmlPreviewEntry)
    {
        return $$"""
        const fs = require('fs');
        const path = require('path');

        function readLocalFile(fileName) {
          const safeName = path.basename(fileName);
          return fs.readFileSync(safeName, 'utf8');
        }

        function isSafeLocalAsset(assetPath, extension) {
          return !/^(?:https?:|data:|\/\/)/i.test(assetPath)
            && path.basename(assetPath).toLowerCase().endsWith(extension);
        }

        function inlineLocalAssets(html) {
          const withStyles = html.replace(/<link\s+[^>]*href=["']([^"']+)["'][^>]*>/gi, (match, href) => {
            const styleName = path.basename(href);
            if (!isSafeLocalAsset(href, '.css') || !fs.existsSync(styleName)) {
              return '';
            }
            return '<style data-sandbox-inline="' + styleName + '">' + fs.readFileSync(styleName, 'utf8') + '</style>';
          });
          return withStyles.replace(/<script\s+[^>]*src=["']([^"']+)["'][^>]*>\s*<\/script>/gi, (_match, src) => {
            const scriptName = path.basename(src);
            if (!isSafeLocalAsset(src, '.js') || !fs.existsSync(scriptName)) {
              return '';
            }
            return '<script data-sandbox-inline="' + scriptName + '">' + fs.readFileSync(scriptName, 'utf8') + '<\/script>';
          });
        }

        test('browser preview render', () => {
          const html = inlineLocalAssets(readLocalFile('{{htmlPreviewEntry}}'));
          fs.writeFileSync('actual.txt', html, 'utf8');
          expect(html).toMatch(/<\/?[a-z][\s\S]*>/i);
          expect(html).not.toMatch(/<(?:script|link)\s+[^>]*(?:src|href)=["']https?:/i);
        });
        """;
    }

    private static string? LimitPreviewDocument(string value)
    {
        const int maximumPreviewLength = 500_000;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maximumPreviewLength ? value : value[..maximumPreviewLength];
    }

    private static bool IsPreviewEntryForLanguage(string? previewEntry, string selectedLanguage)
    {
        if (string.IsNullOrWhiteSpace(previewEntry))
        {
            return false;
        }

        var extension = Path.GetExtension(previewEntry);
        return selectedLanguage is "javascript" or "html"
            ? extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
              || extension.Equals(".html", StringComparison.OrdinalIgnoreCase)
            : extension.Equals(".py", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> GetStarterFilesForLanguage(
        IReadOnlyDictionary<string, Dictionary<string, string>> starterCode,
        string language)
    {
        if (starterCode.TryGetValue(language, out var files) && files.Count > 0)
        {
            return files;
        }

        if (language == "html" && starterCode.TryGetValue("javascript", out var javascriptFiles))
        {
            return javascriptFiles;
        }

        return new Dictionary<string, string>();
    }

    private static string GetSafePythonModuleName(string previewEntry, string fallback)
    {
        var fileName = Path.GetFileNameWithoutExtension(previewEntry);
        return IsSafeIdentifier(fileName) ? fileName : fallback;
    }

    private static string GetSafeJavaScriptModulePath(string previewEntry, string fallback)
    {
        var fileName = Path.GetFileName(previewEntry);
        return IsSafeModuleFile(fileName) ? fileName : fallback;
    }

    private static bool IsSafeIdentifier(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.All(character => char.IsLetterOrDigit(character) || character == '_');
    }

    private static bool IsSafeModuleFile(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.EndsWith(".js", StringComparison.Ordinal)
            && value.All(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.');
    }

    private static bool IsSafeHtmlFile(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            && Path.GetFileName(value).Equals(value, StringComparison.Ordinal)
            && value.All(character => char.IsLetterOrDigit(character) || character is '_' or '-' or '.');
    }
}
