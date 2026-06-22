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
    private const int DraftMaxTokens = 16384;
    private const int MinimumStarterFilesPerLanguage = 3;
    private const int MinimumTaskDescriptionLength = 300;
    private const int MaximumTaskDescriptionWords = 150;
    private const int MinimumPublicTestCases = 2;
    private const int MinimumHiddenTestCases = 2;
    private const int MinimumAdvancedConcerns = 3;
    private const int MinimumFrontendAdvancedConcerns = 2;
    private const int MaximumDraftAttempts = 5;
    private const int MaximumTasksPerType = 5;
    private const int MaximumAssessmentTasks = 12;
    private static readonly string[] TodoPrototypeTerms =
    [
        "todo", "to-do", "task", "due date", "priority", "assignee", "completion", "dependency"
    ];

    private static readonly IReadOnlyDictionary<string, string[]> AdvancedConcernTerms =
        new Dictionary<string, string[]>
        {
            [TaskTypes.FrontendUiExtension] =
            [
                "asynchronous", "async", "persistence", "localstorage", "indexeddb",
                "state machine", "optimistic", "rollback", "debounce", "race condition",
                "accessibility", "keyboard navigation", "error recovery", "offline",
                "caching", "undo", "redo", "conflict resolution"
            ],
            [TaskTypes.RestApiDevelopment] =
            [
                "idempotency", "pagination", "authorization", "authentication", "transaction",
                "concurrency", "optimistic locking", "rate limit", "caching", "audit",
                "versioning", "versioned update", "version token", "row version", "etag", "if-match",
                "validation", "rollback", "partial failure", "dependency", "conflict resolution",
                "conflict handling", "compare-and-swap", "atomic update", "retry"
            ],
            [TaskTypes.DatabaseQuerySchema] =
            [
                "transaction", "window function", "recursive", "constraint", "migration",
                "idempotent", "concurrency", "rollback", "audit", "aggregation",
                "deduplication", "referential integrity", "null", "duplicate"
            ],
            [TaskTypes.BugFix] =
            [
                "race condition", "asynchronous", "caching", "state", "regression",
                "backward compatibility", "transaction", "concurrency", "memory",
                "validation", "error recovery", "partial failure", "dependency", "invariant"
            ]
        };

    private static readonly string[] RequiredTaskTypes =
    [
        TaskTypes.FrontendUiExtension,
        TaskTypes.RestApiDevelopment,
        TaskTypes.DatabaseQuerySchema,
        TaskTypes.BugFix
    ];

    private readonly AiCompletionService completionService;
    private readonly CanonicalPrototypeSource prototypeSource;

    public AssessmentDraftGenerationService(
        AiCompletionService completionService,
        CanonicalPrototypeSource prototypeSource)
    {
        this.completionService = completionService;
        this.prototypeSource = prototypeSource;
    }

    public async Task<IReadOnlyList<Question>> GenerateAssessmentDraftAsync(
        Guid assessmentId,
        AssessmentRequest request,
        CancellationToken cancellationToken)
    {
        var taskTypeCounts = NormalizeTaskTypeCounts(request.TaskTypeCounts);
        var requestedTaskTypes = BuildRequestedTaskTypes(taskTypeCounts);
        var difficulty = NormalizeDifficulty(request.Difficulty);
        var tasks = new List<Question>(requestedTaskTypes.Length);

        for (var index = 0; index < requestedTaskTypes.Length; index += 1)
        {
            var taskType = requestedTaskTypes[index];
            var generated = await GenerateValidatedTasksAsync(
                assessmentId,
                BuildAssessmentTaskPrompt(request, taskType, difficulty, index + 1, requestedTaskTypes.Length),
                [taskType],
                cancellationToken);
            var task = generated.Single();
            task.SortOrder = index + 1;
            task.Difficulty = difficulty;
            task.MaxScore = 100 / requestedTaskTypes.Length + (index < 100 % requestedTaskTypes.Length ? 1 : 0);
            task.StarterPrototypeReference = PrototypeDefaults.TodoListReference;
            tasks.Add(task);
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
        var tasks = await GenerateValidatedTasksAsync(
            assessmentId,
            BuildSingleTaskDraftPrompt(request, taskType, sharedPrototypeReference),
            [taskType],
            cancellationToken);
        var draft = tasks.Single();
        draft.SortOrder = sortOrder;
        draft.Difficulty = NormalizeDifficulty(request.Difficulty);
        draft.LanguageConstraintsJson = JsonDocumentSerializer.Serialize(NormalizeStudentLanguages(request.SupportedLanguages, taskType));
        draft.StarterPrototypeReference = PrototypeDefaults.TodoListReference;
        return draft;
    }

    private async Task<List<Question>> GenerateValidatedTasksAsync(
        Guid assessmentId,
        string basePrompt,
        IReadOnlyCollection<string> expectedTaskTypes,
        CancellationToken cancellationToken)
    {
        string? previousFailure = null;

        for (var attempt = 1; attempt <= MaximumDraftAttempts; attempt += 1)
        {
            var prompt = previousFailure is null
                ? basePrompt
                : BuildCorrectionPrompt(basePrompt, previousFailure, expectedTaskTypes, attempt);
            var result = await completionService.GenerateAsync(
                BuildDraftSystemPrompt(),
                prompt,
                AiResponseFormat.Json,
                cancellationToken,
                DraftMaxTokens);
            EnsureDraftCompletionWasNotTruncated(result);

            try
            {
                return ParseTasks(result.Content, assessmentId, expectedTaskTypes);
            }
            catch (AiDraftGenerationException exception) when (attempt < MaximumDraftAttempts)
            {
                previousFailure = exception.Message;
            }
        }

        throw new AiDraftGenerationException(previousFailure ?? "AI draft generation failed validation.");
    }

    private static string BuildCorrectionPrompt(
        string basePrompt,
        string previousFailure,
        IReadOnlyCollection<string> expectedTaskTypes,
        int attempt)
    {
        var correctionLines = new List<string>
        {
            basePrompt,
            "",
            $"The previous draft was rejected: {previousFailure}",
            "Correct that exact failure. Increase architectural depth instead of merely adding more prose."
        };

        if (previousFailure.Contains("still tutorial-level", StringComparison.OrdinalIgnoreCase))
        {
            var taskType = expectedTaskTypes.Single();
            correctionLines.Add(
                $"In the title and problem description, explicitly name and require at least four distinct concerns from this exact vocabulary: {string.Join(", ", AdvancedConcernTerms[taskType])}.");
            correctionLines.Add(
                "Each named concern must affect implementation behavior or acceptance criteria; do not merely list keywords.");
        }

        if (previousFailure.Contains("missing test code for language", StringComparison.OrdinalIgnoreCase))
        {
            var taskType = expectedTaskTypes.Single();
            var requiredLanguages = NormalizeStudentLanguages(null, taskType);
            correctionLines.Add(
                $"Every public and hidden test_cases item must contain a test_code object with non-empty executable entries for every required language: {string.Join(", ", requiredLanguages)}.");
            correctionLines.Add(
                "Do not return prose, pseudocode, an empty string, or a test entry under a different language key. Preserve at least two public and two hidden executable tests.");
        }

        correctionLines.Add($"Correction attempt: {attempt}-{Guid.NewGuid():N}");
        return string.Join("\n", correctionLines);
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
            "Every task must be a focused extension, backend capability, schema change, or bug fix for the canonical source in assessmentPrototype/. Never invent or regenerate the base application.",
            "The canonical Todo entity has exactly id, title, description, and completed fields. Extensions may add focused fields only when the task explicitly requires the schema/API/UI change.",
            "Preserve the canonical REST routes: GET/POST /api/todos, GET/PUT/DELETE /api/todos/{todo_id}, and POST /api/todos/{todo_id}/toggle.",
            "Preserve the canonical module contracts: browser-safe frontend/index.html, frontend/styles.css, frontend/app.js; FastAPI backend/main.py and controllers.py; Peewee backend/models.py and repositories.py; backend/services.py and schemas.py; SQLite persistence.",
            "Starter code must be a task-focused copy or extension of those canonical contracts. Do not output React, Vite, Next.js, ASP.NET, Flask, SQLAlchemy, an in-memory replacement database, or a different Todo base application.",
            "Set the challenge level substantially above a tutorial or basic CRUD exercise. Even easy tasks must require non-trivial reasoning across modules; hard tasks should resemble a compact senior-level take-home exercise.",
            "Reject trivial themes such as a static card, simple list/filter/sort, one-endpoint CRUD, one-query lookup, or an isolated one-line bug.",
            "Every task must require coordinated changes across at least three editable starter files for every supported language.",
            "Do not generate progress bars, profile cards, theme toggles, basic forms, simple filters/sorts, static dashboards, counters, or isolated CRUD handlers.",
            "Use flat file names without directories so the browser workspace and sandbox can execute them directly.",
            "Starter files must contain a realistic incomplete codebase with existing contracts, partial implementations, and TODOs. Do not provide the completed solution.",
            "Keep the problem description concise: 80 to 150 words maximum.",
            "State the goal, essential behavior, important edge cases, and acceptance criteria without giving students a copy-ready implementation plan.",
            "Require cross-file behavior, input validation, error handling, state or data-flow consistency, and at least one backward-compatibility or regression constraint.",
            "Name and require at least four advanced engineering concerns appropriate to the task type, such as asynchronous coordination, persistence, state machines, idempotency, authorization, pagination, concurrency, transactions, migrations, rollback, caching, accessibility, auditability, or conflict resolution.",
            "Every task must include at least two public and two hidden test cases. Tests must exercise behavior across the provided files, including edge cases and failure paths.",
            "Supported student languages are python, javascript, typescript, html, and sql.",
            "Use html for frontend_ui_extension tasks and sql for database_query_schema tasks unless the administrator explicitly asks for another supported language.",
            "For frontend_ui_extension, use index.html, styles.css, and app.js adapted from the canonical browser-safe UI and require accessible interactions, derived state, responsive behavior, and robust empty/error states.",
            "For rest_api_development, use Python and extend the canonical FastAPI/Peewee files. Require related routes, Pydantic validation, consistent errors, and concurrency/idempotency or pagination/filtering concerns.",
            "For database_query_schema, use Python/Peewee/SQLite files from the canonical backend. SQL test helpers are allowed, but the base ORM and database may not be replaced.",
            "For bug_fix, provide at least three interacting Todo application modules and require diagnosing several related defects while preserving public interfaces and preventing regressions.",
            "Every test case must include non-empty test_code for every language in the task's language_constraints.",
            "For database_query_schema tasks, every public and hidden test case must include a non-empty sql test_code entry that verifies the student's solution.sql file.",
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
            "      \"language_constraints\": [\"python\", \"javascript\", \"typescript\", \"html\", \"sql\"],",
            "      \"starter_code\": { \"python\": {\"solution.py\":\"code\", \"models.py\":\"code\", \"services.py\":\"code\"}, \"javascript\": {\"solution.js\":\"code\", \"models.js\":\"code\", \"services.js\":\"code\"}, \"typescript\": {\"solution.ts\":\"code\", \"types.ts\":\"code\", \"services.ts\":\"code\"}, \"html\": {\"index.html\":\"code\", \"styles.css\":\"code\", \"app.js\":\"code\"}, \"sql\": {\"schema.sql\":\"code\", \"seed.sql\":\"code\", \"solution.sql\":\"code\"} },",
            "      \"starter_files_metadata\": { \"language\": {\"file1\":\"editable\", \"file2\":\"editable\", \"file3\":\"editable\"} },",
            "      \"verification_metadata\": {\"primary_view\":\"string\"},",
            "      \"grading_configuration\": {\"runner\":\"automated_tests\", \"requires_student_install\":\"false\"},",
            "      \"traceability_metadata\": {\"requirements\":\"REQ-18f,REQ-18g,REQ-18h,REQ-18i,REQ-18j\"},",
            "      \"max_score\": 25,",
            "      \"test_cases\": [",
            "        {",
            "          \"name\": \"string\",",
            "          \"visibility\": \"public|hidden\",",
            "          \"test_code\": {\"python\":\"pytest code\", \"javascript\":\"jest code\", \"typescript\":\"jest code\", \"html\":\"jest code that checks HTML files\", \"sql\":\"jest code that checks SQL files\"},",
            "          \"traceability_metadata\": {\"requirements\":\"REQ-52,REQ-53\"}",
            "        }",
            "      ]",
            "    }",
            "  ]",
            "}",
            "",
            "Every task must include at least two public test cases and two hidden test cases."
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

    private static string BuildAssessmentTaskPrompt(
        AssessmentRequest request,
        string taskType,
        string difficulty,
        int taskNumber,
        int totalTasks)
    {
        return string.Join("\n",
        [
            "Generate exactly one task for a larger assessment draft.",
            $"Task position: {taskNumber} of {totalTasks}",
            $"Required task type: {taskType}",
            $"Required difficulty: {difficulty}",
            "Keep this task inside the default Todo List prototype and its shared data model.",
            "Make it distinct from other likely tasks of the same category by choosing a focused Todo subsystem or engineering concern.",
            "",
            $"Assessment title: {request.Title}",
            $"Assessment description: {request.Description}",
            $"Duration minutes: {request.DurationMinutes}",
            $"Shared prototype reference: {PrototypeDefaults.TodoListReference}",
            $"Shared prototype version: {PrototypeDefaults.TodoListVersion}",
            $"Shared prototype metadata: {JsonDocumentSerializer.Serialize(request.SharedPrototypeMetadata ?? new Dictionary<string, string>())}"
        ]);
    }

    private static string BuildSingleTaskDraftPrompt(
        GenerateQuestionDraftRequest request,
        string taskType,
        string? sharedPrototypeReference)
    {
        var languages = NormalizeStudentLanguages(request.SupportedLanguages, taskType);
        return string.Join("\n",
        [
            "Generate exactly one draft task.",
            $"Task type: {taskType}",
            $"Difficulty: {NormalizeDifficulty(request.Difficulty)}",
            $"Supported languages: {string.Join(", ", languages)}",
            $"Shared prototype reference: {PrototypeDefaults.TodoListReference}",
            "The generated task must modify the default Todo List application; unrelated product domains are invalid.",
            $"Administrator guidance: {NormalizeOptionalText(request.ProblemDescriptionMarkdown) ?? "(none supplied)"}"
        ]);
    }

    internal static Dictionary<string, int> NormalizeTaskTypeCounts(Dictionary<string, int>? requestedCounts)
    {
        var normalized = RequiredTaskTypes.ToDictionary(
            taskType => taskType,
            taskType => Math.Clamp(requestedCounts?.GetValueOrDefault(taskType) ?? 1, 0, MaximumTasksPerType));
        var total = normalized.Values.Sum();

        if (total == 0)
        {
            normalized[TaskTypes.FrontendUiExtension] = 1;
            total = 1;
        }

        if (total <= MaximumAssessmentTasks)
        {
            return normalized;
        }

        var overflow = total - MaximumAssessmentTasks;
        foreach (var taskType in RequiredTaskTypes.Reverse())
        {
            var removable = Math.Min(normalized[taskType], overflow);
            normalized[taskType] -= removable;
            overflow -= removable;
            if (overflow == 0)
            {
                break;
            }
        }

        return normalized;
    }

    internal static string[] BuildRequestedTaskTypes(IReadOnlyDictionary<string, int> taskTypeCounts)
    {
        return RequiredTaskTypes
            .SelectMany(taskType => Enumerable.Repeat(taskType, taskTypeCounts.GetValueOrDefault(taskType)))
            .ToArray();
    }

    private List<Question> ParseTasks(string json, Guid assessmentId, IReadOnlyCollection<string> expectedTaskTypes)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("tasks", out var tasksElement)
                || tasksElement.ValueKind != JsonValueKind.Array)
            {
                throw new AiDraftGenerationException("AI draft response did not include a tasks array.");
            }

            var expectedTypes = expectedTaskTypes.ToArray();
            var tasks = tasksElement.EnumerateArray()
                .Select((element, index) => ParseQuestion(
                    element,
                    assessmentId,
                    index + 1,
                    index < expectedTypes.Length ? expectedTypes[index] : null))
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

    private Question ParseQuestion(
        JsonElement element,
        Guid assessmentId,
        int sortOrder,
        string? expectedTaskType)
    {
        var title = RequiredString(element, "title");
        var taskType = NormalizeTaskType(RequiredString(element, "task_type"));
        if (expectedTaskType is not null && taskType != expectedTaskType)
        {
            throw new AiDraftGenerationException(
                $"Generated task '{title}' returned task type '{taskType}' but the required task type is '{expectedTaskType}'.");
        }
        var verificationMode = NormalizeVerificationMode(OptionalString(element, "verification_mode"), taskType);
        var starterCode = RequiredNestedStringDictionary(element, "starter_code");
        var languageConstraints = NormalizeStudentLanguages(ReadStringArray(element, "language_constraints"), taskType);
        var questionId = Guid.NewGuid();
        var testCases = RequiredArray(element, "test_cases")
            .Select(ParseTestCase)
            .ToList();
        var problemDescription = RequiredString(element, "problem_description_markdown");

        ValidateTestCodeCoverage(title, languageConstraints, testCases);
        ValidateAdvancedTaskStructure(
            title,
            taskType,
            problemDescription,
            languageConstraints,
            starterCode,
            testCases);
        starterCode = prototypeSource.ApplyCanonicalFiles(starterCode, languageConstraints);
        var starterMetadata = MergeStarterMetadata(
            starterCode,
            ReadNestedStringDictionary(element, "starter_files_metadata"));

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
            StarterPrototypeReference = PrototypeDefaults.TodoListReference,
            ProblemDescriptionMarkdown = problemDescription,
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(languageConstraints),
            StarterCodeJson = JsonDocumentSerializer.Serialize(starterCode),
            StarterFilesMetadataJson = JsonDocumentSerializer.Serialize(starterMetadata),
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

    private static void ValidateTestCodeCoverage(
        string title,
        IReadOnlyCollection<string> languageConstraints,
        IReadOnlyCollection<TestCase> testCases)
    {
        foreach (var testCase in testCases)
        {
            var testCode = JsonDocumentSerializer.Deserialize(testCase.TestCodeJson, new Dictionary<string, string>());
            foreach (var language in languageConstraints)
            {
                if (HasNonEmptyTestCode(testCode, language))
                {
                    continue;
                }

                throw new AiDraftGenerationException(
                    $"Generated task '{title}' test case '{testCase.Name}' is missing test code for language '{language}'. Regenerate the draft so the LLM provides complete tests.");
            }
        }
    }

    internal static bool HasNonEmptyTestCode(Dictionary<string, string> testCode, string language)
    {
        if (testCode.TryGetValue(language, out var direct) && !string.IsNullOrWhiteSpace(direct))
        {
            return true;
        }

        if (testCode.TryGetValue(language.ToLowerInvariant(), out var normalized) && !string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        if (language == "html"
            && testCode.TryGetValue("javascript", out var javascript)
            && !string.IsNullOrWhiteSpace(javascript))
        {
            return true;
        }

        return language == "javascript"
               && testCode.TryGetValue("html", out var html)
               && !string.IsNullOrWhiteSpace(html);
    }

    private static void ValidateAdvancedTaskStructure(
        string title,
        string taskType,
        string problemDescription,
        IReadOnlyCollection<string> languageConstraints,
        IReadOnlyDictionary<string, Dictionary<string, string>> starterCode,
        IReadOnlyCollection<TestCase> testCases)
    {
        if (problemDescription.Trim().Length < MinimumTaskDescriptionLength)
        {
            throw new AiDraftGenerationException(
                $"Generated task '{title}' is too shallow. Its problem description must be at least {MinimumTaskDescriptionLength} characters and specify requirements, constraints, edge cases, and acceptance criteria.");
        }

        var normalizedDescription = problemDescription.ToLowerInvariant();
        if (!TodoPrototypeTerms.Any(term => normalizedDescription.Contains(term, StringComparison.Ordinal)))
        {
            throw new AiDraftGenerationException(
                $"Generated task '{title}' is not anchored to the default Todo List prototype.");
        }

        var normalizedStarterCode = string.Join(
            "\n",
            starterCode.Values.SelectMany(files => files.Values))
            .ToLowerInvariant();
        if (!TodoPrototypeTerms.Any(term => normalizedStarterCode.Contains(term, StringComparison.Ordinal)))
        {
            throw new AiDraftGenerationException(
                $"Generated task '{title}' starter files are not derived from the default Todo List prototype.");
        }

        // Titles often carry concise architectural requirements because student-facing
        // descriptions are intentionally limited to 150 words. Validate both fields.
        var normalizedAdvancedTaskText = $"{title}\n{problemDescription}".ToLowerInvariant();
        var advancedConcernCount = CountAdvancedConcerns(normalizedAdvancedTaskText, taskType);
        var minimumAdvancedConcerns = GetMinimumAdvancedConcerns(taskType);
        if (advancedConcernCount < minimumAdvancedConcerns)
        {
            throw new AiDraftGenerationException(
                $"Generated task '{title}' is still tutorial-level. It must explicitly require at least {minimumAdvancedConcerns} advanced {taskType} concerns such as concurrency, persistence, transactions, rollback, accessibility, caching, or conflict handling.");
        }

        var descriptionWordCount = problemDescription
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Length;
        if (descriptionWordCount > MaximumTaskDescriptionWords)
        {
            throw new AiDraftGenerationException(
                $"Generated task '{title}' is too detailed. Its problem description must contain no more than {MaximumTaskDescriptionWords} words.");
        }

        foreach (var language in languageConstraints)
        {
            if (!starterCode.TryGetValue(language, out var files)
                || files.Count < MinimumStarterFilesPerLanguage
                || files.Any(file => string.IsNullOrWhiteSpace(file.Key) || string.IsNullOrWhiteSpace(file.Value)))
            {
                throw new AiDraftGenerationException(
                    $"Generated task '{title}' must provide at least {MinimumStarterFilesPerLanguage} non-empty starter files for language '{language}'.");
            }
        }

        var publicCount = testCases.Count(testCase => testCase.Visibility == TestCaseVisibilities.Public);
        var hiddenCount = testCases.Count(testCase => testCase.Visibility == TestCaseVisibilities.Hidden);
        if (publicCount < MinimumPublicTestCases || hiddenCount < MinimumHiddenTestCases)
        {
            throw new AiDraftGenerationException(
                $"Generated task '{title}' must include at least {MinimumPublicTestCases} public and {MinimumHiddenTestCases} hidden test cases.");
        }
    }

    internal static int CountAdvancedConcerns(string taskText, string taskType)
    {
        var normalizedTaskType = NormalizeTaskType(taskType);
        var normalizedText = taskText.ToLowerInvariant();
        return AdvancedConcernTerms[normalizedTaskType]
            .Count(term => normalizedText.Contains(term, StringComparison.Ordinal));
    }

    internal static int GetMinimumAdvancedConcerns(string taskType)
    {
        return NormalizeTaskType(taskType) == TaskTypes.FrontendUiExtension
            ? MinimumFrontendAdvancedConcerns
            : MinimumAdvancedConcerns;
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

    private static Dictionary<string, Dictionary<string, string>> MergeStarterMetadata(
        Dictionary<string, Dictionary<string, string>> starterCode,
        Dictionary<string, Dictionary<string, string>>? generatedMetadata)
    {
        var metadata = BuildStarterMetadata(starterCode);
        if (generatedMetadata is null)
        {
            return metadata;
        }

        foreach (var language in generatedMetadata)
        {
            if (!metadata.TryGetValue(language.Key, out var files))
            {
                continue;
            }

            foreach (var file in language.Value)
            {
                if (files.ContainsKey(file.Key))
                {
                    files[file.Key] = file.Value;
                }
            }
        }

        return metadata;
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

    private static string[] NormalizeStudentLanguages(string[]? languages, string? taskType = null)
    {
        var normalizedLanguages = (languages ?? [])
            .Select(NormalizeLanguage)
            .Where(language => language is "python" or "javascript" or "typescript" or "html" or "sql")
            .Distinct()
            .ToArray();

        var effectiveLanguages = normalizedLanguages.Length > 0
            ? normalizedLanguages
            : taskType switch
            {
                TaskTypes.FrontendUiExtension => ["html"],
                TaskTypes.DatabaseQuerySchema => ["sql"],
                _ => ["python", "javascript"]
            };

        if (taskType == TaskTypes.FrontendUiExtension && !effectiveLanguages.Contains("html"))
        {
            return ["html"];
        }

        if (taskType == TaskTypes.DatabaseQuerySchema && !effectiveLanguages.Contains("sql"))
        {
            return ["sql"];
        }

        return effectiveLanguages;
    }

    private static string NormalizeLanguage(string language)
    {
        return language.Trim().ToLowerInvariant() switch
        {
            "js" => "javascript",
            "javascript" => "javascript",
            "ts" => "typescript",
            "typescript" => "typescript",
            "html" => "html",
            "sql" => "sql",
            "py" => "python",
            "python" => "python",
            _ => "python"
        };
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
