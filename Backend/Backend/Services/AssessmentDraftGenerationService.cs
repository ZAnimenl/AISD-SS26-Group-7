using System.Text.Json;
using Backend.Contracts;
using Backend.Domain;

namespace Backend.Services;

public sealed class AiDraftGenerationException : Exception
{
    public AiDraftGenerationException(string message)
        : base(message)
    {
    }
}

public sealed class AssessmentDraftGenerationService
{
    private const int DraftMaxTokens = 8192;

    private static readonly string[] RequiredTaskTypes =
    [
        TaskTypes.FrontendUiExtension,
        TaskTypes.RestApiDevelopment,
        TaskTypes.DatabaseQuerySchema,
        TaskTypes.BugFix
    ];

    private readonly AiCompletionService completionService;

    public AssessmentDraftGenerationService(AiCompletionService completionService)
    {
        this.completionService = completionService;
    }

    public async Task<IReadOnlyList<Question>> GenerateAssessmentDraftAsync(
        Guid assessmentId,
        AssessmentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await completionService.GenerateAsync(
            BuildDraftSystemPrompt(),
            BuildAssessmentDraftPrompt(request),
            AiResponseFormat.Json,
            cancellationToken,
            DraftMaxTokens);
        EnsureDraftCompletionWasNotTruncated(result);
        var tasks = ParseTasks(result.Content, assessmentId, expectedTaskTypes: RequiredTaskTypes);

        for (var index = 0; index < tasks.Count; index += 1)
        {
            tasks[index].SortOrder = index + 1;
            tasks[index].MaxScore = tasks[index].MaxScore <= 0 ? 25 : tasks[index].MaxScore;
        }

        return tasks;
    }

    public async Task<Question> GenerateQuestionDraftAsync(
        Guid assessmentId,
        GenerateQuestionDraftRequest request,
        string? sharedPrototypeReference,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        var taskType = NormalizeTaskType(request.TaskType);
        var result = await completionService.GenerateAsync(
            BuildDraftSystemPrompt(),
            BuildSingleTaskDraftPrompt(request, taskType, sharedPrototypeReference),
            AiResponseFormat.Json,
            cancellationToken,
            DraftMaxTokens);
        EnsureDraftCompletionWasNotTruncated(result);
        var tasks = ParseTasks(result.Content, assessmentId, expectedTaskTypes: [taskType]);
        var draft = tasks.Single();
        draft.SortOrder = sortOrder;
        draft.Difficulty = NormalizeDifficulty(request.Difficulty);
        draft.LanguageConstraintsJson = JsonDocumentSerializer.Serialize(NormalizeStudentLanguages(request.SupportedLanguages));
        draft.StarterPrototypeReference = NormalizeOptionalText(request.StarterPrototypeReference)
            ?? NormalizeOptionalText(sharedPrototypeReference);
        return draft;
    }

    private static string BuildDraftSystemPrompt()
    {
        return string.Join("\n",
        [
            "You generate coding assessment draft tasks for administrator review.",
            "Return only valid JSON. Do not wrap the JSON in Markdown.",
            "The administrator must review every generated task and test before publication.",
            "Do not include provider secrets, hidden grading explanations, or any student-specific data.",
            "Generated tasks must be practical browser-workspace tasks, not algorithm puzzle tasks.",
            "Supported student languages are python and javascript.",
            "",
            "JSON shape:",
            "{",
            "  \"tasks\": [",
            "    {",
            "      \"title\": \"string\",",
            "      \"task_type\": \"frontend_ui_extension|rest_api_development|database_query_schema|bug_fix\",",
            "      \"difficulty\": \"easy|medium|hard\",",
            "      \"verification_mode\": \"browser_ui_preview|api_response_check|database_result_check|automated_test|regression_test\",",
            "      \"starter_prototype_reference\": \"string or null\",",
            "      \"problem_description_markdown\": \"string\",",
            "      \"language_constraints\": [\"python\", \"javascript\"],",
            "      \"starter_code\": { \"python\": {\"file.py\":\"code\"}, \"javascript\": {\"file.js\":\"code\"} },",
            "      \"starter_files_metadata\": { \"python\": {\"file.py\":\"editable\"}, \"javascript\": {\"file.js\":\"editable\"} },",
            "      \"verification_metadata\": {\"primary_view\":\"string\"},",
            "      \"grading_configuration\": {\"runner\":\"automated_tests\", \"requires_student_install\":\"false\"},",
            "      \"traceability_metadata\": {\"requirements\":\"REQ-18f,REQ-18g,REQ-18h,REQ-18i,REQ-18j\"},",
            "      \"max_score\": 25,",
            "      \"test_cases\": [",
            "        {",
            "          \"name\": \"string\",",
            "          \"visibility\": \"public|hidden\",",
            "          \"test_code\": {\"python\":\"pytest code\", \"javascript\":\"jest code\"},",
            "          \"traceability_metadata\": {\"requirements\":\"REQ-52,REQ-53\"}",
            "        }",
            "      ]",
            "    }",
            "  ]",
            "}",
            "",
            "Every task must include at least one public test case and one hidden test case."
        ]);
    }

    private static void EnsureDraftCompletionWasNotTruncated(AiCompletionResult result)
    {
        if (string.Equals(result.FinishReason, "length", StringComparison.OrdinalIgnoreCase))
        {
            throw new AiDraftGenerationException(
                "AI draft generation was cut off by the provider output limit. Try a shorter assessment description or generate one task draft at a time.");
        }
    }

    private static string BuildAssessmentDraftPrompt(AssessmentRequest request)
    {
        return string.Join("\n",
        [
            "Generate one complete draft assessment with exactly four tasks.",
            "The four task types must be exactly: frontend_ui_extension, rest_api_development, database_query_schema, bug_fix.",
            "Use the provided assessment information and shared prototype context.",
            "",
            $"Assessment title: {request.Title}",
            $"Assessment description: {request.Description}",
            $"Duration minutes: {request.DurationMinutes}",
            $"Shared prototype reference: {NormalizeOptionalText(request.SharedPrototypeReference) ?? "(none supplied)"}",
            $"Shared prototype version: {NormalizeOptionalText(request.SharedPrototypeVersion) ?? "(none supplied)"}",
            $"Shared prototype metadata: {JsonDocumentSerializer.Serialize(request.SharedPrototypeMetadata ?? new Dictionary<string, string>())}"
        ]);
    }

    private static string BuildSingleTaskDraftPrompt(
        GenerateQuestionDraftRequest request,
        string taskType,
        string? sharedPrototypeReference)
    {
        var languages = NormalizeStudentLanguages(request.SupportedLanguages);
        return string.Join("\n",
        [
            "Generate exactly one draft task.",
            $"Task type: {taskType}",
            $"Difficulty: {NormalizeDifficulty(request.Difficulty)}",
            $"Supported languages: {string.Join(", ", languages)}",
            $"Shared prototype reference: {NormalizeOptionalText(request.StarterPrototypeReference) ?? NormalizeOptionalText(sharedPrototypeReference) ?? "(none supplied)"}"
        ]);
    }

    private static List<Question> ParseTasks(string json, Guid assessmentId, IReadOnlyCollection<string> expectedTaskTypes)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("tasks", out var tasksElement)
                || tasksElement.ValueKind != JsonValueKind.Array)
            {
                throw new AiDraftGenerationException("AI draft response did not include a tasks array.");
            }

            var tasks = tasksElement.EnumerateArray()
                .Select((element, index) => ParseQuestion(element, assessmentId, index + 1))
                .ToList();

            if (tasks.Count != expectedTaskTypes.Count)
            {
                throw new AiDraftGenerationException("AI draft response returned the wrong number of tasks.");
            }

            var actualTaskTypes = tasks.Select(task => task.TaskType).ToHashSet();
            if (!expectedTaskTypes.All(actualTaskTypes.Contains))
            {
                throw new AiDraftGenerationException("AI draft response did not return the requested task categories.");
            }

            return tasks;
        }
        catch (JsonException exception)
        {
            throw new AiDraftGenerationException($"AI draft response was not valid JSON: {exception.Message}");
        }
    }

    private static Question ParseQuestion(JsonElement element, Guid assessmentId, int sortOrder)
    {
        var title = RequiredString(element, "title");
        var taskType = NormalizeTaskType(RequiredString(element, "task_type"));
        var verificationMode = NormalizeVerificationMode(OptionalString(element, "verification_mode"), taskType);
        var starterCode = RequiredNestedStringDictionary(element, "starter_code");
        var languageConstraints = NormalizeStudentLanguages(ReadStringArray(element, "language_constraints"));
        var questionId = Guid.NewGuid();
        var testCases = RequiredArray(element, "test_cases")
            .Select(ParseTestCase)
            .ToList();

        if (!testCases.Any(testCase => testCase.Visibility == TestCaseVisibilities.Public)
            || !testCases.Any(testCase => testCase.Visibility == TestCaseVisibilities.Hidden))
        {
            throw new AiDraftGenerationException($"Generated task '{title}' must include public and hidden tests.");
        }

        foreach (var testCase in testCases)
        {
            testCase.QuestionId = questionId;
        }

        return new Question
        {
            Id = questionId,
            AssessmentId = assessmentId,
            Title = title,
            TaskType = taskType,
            Difficulty = NormalizeDifficulty(OptionalString(element, "difficulty")),
            VerificationMode = verificationMode,
            StarterPrototypeReference = NormalizeOptionalText(OptionalString(element, "starter_prototype_reference")),
            ProblemDescriptionMarkdown = RequiredString(element, "problem_description_markdown"),
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(languageConstraints),
            StarterCodeJson = JsonDocumentSerializer.Serialize(starterCode),
            StarterFilesMetadataJson = JsonDocumentSerializer.Serialize(
                ReadNestedStringDictionary(element, "starter_files_metadata") ?? BuildStarterMetadata(starterCode)),
            VerificationMetadataJson = JsonDocumentSerializer.Serialize(
                ReadStringDictionary(element, "verification_metadata") ?? new Dictionary<string, string> { ["primary_view"] = verificationMode }),
            GradingConfigurationJson = JsonDocumentSerializer.Serialize(
                ReadStringDictionary(element, "grading_configuration") ?? new Dictionary<string, string>
                {
                    ["runner"] = "automated_tests",
                    ["requires_student_install"] = "false"
                }),
            AuthoringSource = AuthoringSources.LlmGenerated,
            TraceabilityMetadataJson = JsonDocumentSerializer.Serialize(AddDraftTraceability(
                ReadStringDictionary(element, "traceability_metadata"))),
            SortOrder = sortOrder,
            MaxScore = OptionalInt(element, "max_score") ?? 25,
            TestCases = testCases
        };
    }

    private static TestCase ParseTestCase(JsonElement element)
    {
        var visibility = OptionalString(element, "visibility") == TestCaseVisibilities.Hidden
            ? TestCaseVisibilities.Hidden
            : TestCaseVisibilities.Public;

        return new TestCase
        {
            Id = Guid.NewGuid(),
            Name = RequiredString(element, "name"),
            Visibility = visibility,
            TestCodeJson = JsonDocumentSerializer.Serialize(RequiredStringDictionary(element, "test_code")),
            AuthoringSource = AuthoringSources.LlmGenerated,
            PublicMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["student_visible"] = visibility == TestCaseVisibilities.Public ? "true" : "false"
            }),
            AdminMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["review_status"] = "administrator_review_required"
            }),
            TraceabilityMetadataJson = JsonDocumentSerializer.Serialize(AddDraftTraceability(
                ReadStringDictionary(element, "traceability_metadata")))
        };
    }

    private static Dictionary<string, string> AddDraftTraceability(Dictionary<string, string>? metadata)
    {
        var next = metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(metadata);
        next["source"] = "llm_generated";
        next["review_status"] = "administrator_review_required";
        return next;
    }

    private static JsonElement.ArrayEnumerator RequiredArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new AiDraftGenerationException($"AI draft task is missing array property '{propertyName}'.");
        }

        return property.EnumerateArray();
    }

    private static string RequiredString(JsonElement element, string propertyName)
    {
        var value = OptionalString(element, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new AiDraftGenerationException($"AI draft task is missing string property '{propertyName}'.");
        }

        return value;
    }

    private static string? OptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int? OptionalInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetInt32()
            : null;
    }

    private static string[] ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .ToArray();
    }

    private static Dictionary<string, string> RequiredStringDictionary(JsonElement element, string propertyName)
    {
        return ReadStringDictionary(element, propertyName)
            ?? throw new AiDraftGenerationException($"AI draft task is missing object property '{propertyName}'.");
    }

    private static Dictionary<string, string>? ReadStringDictionary(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return property.EnumerateObject().ToDictionary(
            item => item.Name,
            item => item.Value.ValueKind == JsonValueKind.String
                ? item.Value.GetString() ?? string.Empty
                : item.Value.GetRawText());
    }

    private static Dictionary<string, Dictionary<string, string>> RequiredNestedStringDictionary(JsonElement element, string propertyName)
    {
        return ReadNestedStringDictionary(element, propertyName)
            ?? throw new AiDraftGenerationException($"AI draft task is missing nested object property '{propertyName}'.");
    }

    private static Dictionary<string, Dictionary<string, string>>? ReadNestedStringDictionary(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return property.EnumerateObject().ToDictionary(
            language => NormalizeLanguage(language.Name),
            language => language.Value.ValueKind == JsonValueKind.Object
                ? language.Value.EnumerateObject().ToDictionary(
                    file => file.Name,
                    file => file.Value.ValueKind == JsonValueKind.String
                        ? file.Value.GetString() ?? string.Empty
                        : file.Value.GetRawText())
                : new Dictionary<string, string>());
    }

    private static Dictionary<string, Dictionary<string, string>> BuildStarterMetadata(Dictionary<string, Dictionary<string, string>> starterCode)
    {
        return starterCode.ToDictionary(
            language => language.Key,
            language => language.Value.ToDictionary(file => file.Key, _ => "editable"));
    }

    private static string NormalizeTaskType(string? taskType)
    {
        return taskType switch
        {
            TaskTypes.FrontendUiExtension => TaskTypes.FrontendUiExtension,
            TaskTypes.RestApiDevelopment => TaskTypes.RestApiDevelopment,
            TaskTypes.DatabaseQuerySchema => TaskTypes.DatabaseQuerySchema,
            TaskTypes.BugFix => TaskTypes.BugFix,
            TaskTypes.LegacyWebApplication => TaskTypes.FrontendUiExtension,
            TaskTypes.LegacyApiDevelopment => TaskTypes.RestApiDevelopment,
            TaskTypes.LegacyDatabaseTask => TaskTypes.DatabaseQuerySchema,
            _ => TaskTypes.RestApiDevelopment
        };
    }

    private static string NormalizeVerificationMode(string? verificationMode, string taskType)
    {
        if (verificationMode is VerificationModes.BrowserUiPreview
            or VerificationModes.ApiResponseCheck
            or VerificationModes.DatabaseResultCheck
            or VerificationModes.AutomatedTest
            or VerificationModes.RegressionTest)
        {
            return verificationMode;
        }

        return taskType switch
        {
            TaskTypes.FrontendUiExtension => VerificationModes.BrowserUiPreview,
            TaskTypes.RestApiDevelopment => VerificationModes.ApiResponseCheck,
            TaskTypes.DatabaseQuerySchema => VerificationModes.DatabaseResultCheck,
            TaskTypes.BugFix => VerificationModes.RegressionTest,
            _ => VerificationModes.AutomatedTest
        };
    }

    private static string NormalizeDifficulty(string? difficulty)
    {
        return difficulty switch
        {
            "easy" => "easy",
            "medium" => "medium",
            "hard" => "hard",
            _ => "medium"
        };
    }

    private static string[] NormalizeStudentLanguages(string[]? languages)
    {
        var normalizedLanguages = (languages ?? [])
            .Select(NormalizeLanguage)
            .Where(language => language is "python" or "javascript")
            .Distinct()
            .ToArray();

        return normalizedLanguages.Length > 0 ? normalizedLanguages : ["python", "javascript"];
    }

    private static string NormalizeLanguage(string language)
    {
        return language.Trim().ToLowerInvariant() switch
        {
            "js" => "javascript",
            "javascript" => "javascript",
            _ => "python"
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
