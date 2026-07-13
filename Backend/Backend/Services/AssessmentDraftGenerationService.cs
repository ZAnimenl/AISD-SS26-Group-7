using System.Runtime.ExceptionServices;
using System.Text.Json;
using Backend.Contracts;
using Backend.Domain;

namespace Backend.Services;

public sealed class AiDraftGenerationException : Exception
{
    public AiDraftGenerationException(string message, IReadOnlyCollection<string>? requiredLanguages = null)
        : base(message)
    {
        RequiredLanguages = requiredLanguages?.ToArray() ?? [];
    }

    public IReadOnlyCollection<string> RequiredLanguages { get; }
}

public sealed class AssessmentDraftGenerationService
{
    private const int DraftMaxTokens = 16384;
    // 2 is the natural floor: a Frontend UI task is index.html + app.js,
    // a REST API task is server.js + a data helper, a Bug-fix task is the
    // buggy module + a reference file. Requiring 3 forced the LLM to invent
    // a filler file (usually an unused stylesheet) or fail generation
    // outright, which is what admins were hitting.
    private const int MinimumStarterFilesPerLanguage = 2;
    private const int MinimumTaskDescriptionLength = 300;
    private const int MaximumTaskDescriptionWords = 150;
    private const int MinimumPublicTestCases = 2;
    private const int MinimumHiddenTestCases = 2;
    private const int MinimumAdvancedConcerns = 3;
    private const int MinimumFrontendAdvancedConcerns = 2;
    private const int MaximumDraftAttempts = 5;
    private const int MaximumConcurrentDraftPipelines = 2;
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
    private readonly TokenEfficiencyReferenceBaselineService tokenEfficiencyBaselineService;
    private readonly SemaphoreSlim draftPipelineGate = new(MaximumConcurrentDraftPipelines, MaximumConcurrentDraftPipelines);

    public AssessmentDraftGenerationService(
        AiCompletionService completionService,
        CanonicalPrototypeSource prototypeSource,
        TokenEfficiencyReferenceBaselineService tokenEfficiencyBaselineService)
    {
        this.completionService = completionService;
        this.prototypeSource = prototypeSource;
        this.tokenEfficiencyBaselineService = tokenEfficiencyBaselineService;
    }

    public async Task<IReadOnlyList<Question>> GenerateAssessmentDraftAsync(
        Guid assessmentId,
        AssessmentRequest request,
        CancellationToken cancellationToken)
    {
        var taskTypeCounts = NormalizeTaskTypeCounts(request.TaskTypeCounts);
        var requestedTaskTypes = BuildRequestedTaskTypes(taskTypeCounts);
        var difficulty = NormalizeDifficulty(request.Difficulty);
        var generationJobs = requestedTaskTypes
            .Select((taskType, index) => new Func<CancellationToken, Task<Question>>(async phaseToken =>
            {
                var requiredLanguages = NormalizeStudentLanguages(null, taskType);
                var generated = await GenerateValidatedTasksAsync(
                    assessmentId,
                    AssessmentDraftPromptFactory.BuildAssessmentTaskPrompt(request, taskType, difficulty, requiredLanguages, index + 1, requestedTaskTypes.Length),
                    [taskType],
                    requiredLanguages,
                    phaseToken);
                return generated.Single();
            }))
            .ToArray();
        var tasks = await RunBoundedPhaseAsync(generationJobs, cancellationToken);

        for (var index = 0; index < tasks.Length; index += 1)
        {
            var task = tasks[index];
            task.SortOrder = index + 1;
            task.Difficulty = difficulty;
            task.MaxScore = 100 / requestedTaskTypes.Length + (index < 100 % requestedTaskTypes.Length ? 1 : 0);
            task.StarterPrototypeReference = PrototypeDefaults.TodoListReference;
        }

        var baselineJobs = tasks
            .Select(task => new Func<CancellationToken, Task<Question>>(async phaseToken =>
            {
                await AttachReferenceBaselineAsync(task, phaseToken);
                return task;
            }))
            .ToArray();
        await RunBoundedPhaseAsync(baselineJobs, cancellationToken);

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
        var requiredLanguages = NormalizeStudentLanguages(request.SupportedLanguages, taskType);
        var tasks = await RunWithDraftSlotAsync(
            phaseToken => GenerateValidatedTasksAsync(
                assessmentId,
                AssessmentDraftPromptFactory.BuildSingleTaskDraftPrompt(
                    taskType,
                    NormalizeDifficulty(request.Difficulty),
                    requiredLanguages,
                    NormalizeOptionalText(request.ProblemDescriptionMarkdown)),
                [taskType],
                requiredLanguages,
                phaseToken),
            cancellationToken);
        var draft = tasks.Single();
        draft.SortOrder = sortOrder;
        draft.Difficulty = NormalizeDifficulty(request.Difficulty);
        draft.StarterPrototypeReference = PrototypeDefaults.TodoListReference;
        await RunWithDraftSlotAsync(async phaseToken =>
        {
            await AttachReferenceBaselineAsync(draft, phaseToken);
            return true;
        }, cancellationToken);
        return draft;
    }

    private async Task AttachReferenceBaselineAsync(Question question, CancellationToken cancellationToken)
    {
        var baseline = await tokenEfficiencyBaselineService.RunAsync(question, cancellationToken);
        TaskAiUsageBenchmarkFactory.AttachReferenceBaseline(question, baseline);
    }

    private async Task<List<Question>> GenerateValidatedTasksAsync(
        Guid assessmentId,
        string basePrompt,
        IReadOnlyCollection<string> expectedTaskTypes,
        IReadOnlyCollection<string> requiredLanguages,
        CancellationToken cancellationToken)
    {
        string? previousFailure = null;
        IReadOnlyCollection<string>? previousFailureRequiredLanguages = null;

        for (var attempt = 1; attempt <= MaximumDraftAttempts; attempt += 1)
        {
            var prompt = previousFailure is null
                ? basePrompt
                : BuildCorrectionPrompt(
                    basePrompt,
                    previousFailure,
                    previousFailureRequiredLanguages,
                    expectedTaskTypes,
                    attempt);
            var result = await completionService.GenerateAsync(
                AssessmentDraftPromptFactory.BuildSystemPrompt(MinimumStarterFilesPerLanguage),
                prompt,
                AiResponseFormat.Json,
                cancellationToken,
                DraftMaxTokens);
            EnsureDraftCompletionWasNotTruncated(result);

            try
            {
                return ParseTasks(result.Content, assessmentId, expectedTaskTypes, requiredLanguages);
            }
            catch (AiDraftGenerationException exception) when (attempt < MaximumDraftAttempts)
            {
                previousFailure = exception.Message;
                previousFailureRequiredLanguages = exception.RequiredLanguages;
            }
        }

        throw new AiDraftGenerationException(previousFailure ?? "AI draft generation failed validation.");
    }

    private async Task<T[]> RunBoundedPhaseAsync<T>(
        IReadOnlyCollection<Func<CancellationToken, Task<T>>> jobs,
        CancellationToken cancellationToken)
    {
        using var phaseCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var failureLock = new object();
        ExceptionDispatchInfo? firstFailure = null;
        var tasks = jobs.Select(async job =>
        {
            try
            {
                return await RunWithDraftSlotAsync(job, phaseCancellation.Token);
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                lock (failureLock)
                {
                    firstFailure ??= ExceptionDispatchInfo.Capture(exception);
                }
                await phaseCancellation.CancelAsync();
                throw;
            }
        }).ToArray();

        try
        {
            return await Task.WhenAll(tasks);
        }
        catch when (firstFailure is not null)
        {
            firstFailure.Throw();
            throw;
        }
    }

    private async Task<T> RunWithDraftSlotAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await draftPipelineGate.WaitAsync(cancellationToken);
        try
        {
            return await operation(cancellationToken);
        }
        finally
        {
            draftPipelineGate.Release();
        }
    }

    private static string BuildCorrectionPrompt(
        string basePrompt,
        string previousFailure,
        IReadOnlyCollection<string>? previousFailureRequiredLanguages,
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
            var requiredLanguages = previousFailureRequiredLanguages is { Count: > 0 }
                ? previousFailureRequiredLanguages
                : NormalizeStudentLanguages(null, taskType);
            correctionLines.Add(
                $"Every public and hidden test_cases item must contain a test_code object with non-empty executable entries for every required language: {string.Join(", ", requiredLanguages)}.");
            correctionLines.Add(
                "Do not return prose, pseudocode, an empty string, or a test entry under a different language key. Preserve at least two public and two hidden executable tests.");
        }

        correctionLines.Add($"Correction attempt: {attempt}-{Guid.NewGuid():N}");
        return string.Join("\n", correctionLines);
    }

    private static void EnsureDraftCompletionWasNotTruncated(AiCompletionResult result)
    {
        if (string.Equals(result.FinishReason, "length", StringComparison.OrdinalIgnoreCase))
        {
            throw new AiDraftGenerationException(
                "AI draft generation was cut off by the provider output limit. Try a shorter assessment description or generate one task draft at a time.");
        }
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

    private List<Question> ParseTasks(
        string json,
        Guid assessmentId,
        IReadOnlyCollection<string> expectedTaskTypes,
        IReadOnlyCollection<string> requiredLanguages)
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
                    index < expectedTypes.Length ? expectedTypes[index] : null,
                    requiredLanguages))
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
        string? expectedTaskType,
        IReadOnlyCollection<string> requiredLanguages)
    {
        var title = RequiredString(element, "title");
        var taskType = NormalizeTaskType(RequiredString(element, "task_type"));
        if (expectedTaskType is not null && taskType != expectedTaskType)
        {
            throw new AiDraftGenerationException(
                $"Generated task '{title}' returned task type '{taskType}' but the required task type is '{expectedTaskType}'.");
        }
        var verificationMode = NormalizeVerificationMode(OptionalString(element, "verification_mode"), taskType);
        var difficulty = NormalizeDifficulty(OptionalString(element, "difficulty"));
        var starterCode = RequiredNestedStringDictionary(element, "starter_code");
        var returnedLanguages = NormalizeStudentLanguages(ReadStringArray(element, "language_constraints"), taskType);
        var languageConstraints = requiredLanguages.ToArray();
        if (!returnedLanguages.ToHashSet().SetEquals(languageConstraints))
        {
            throw new AiDraftGenerationException(
                $"Generated task '{title}' must use exactly the requested languages: {string.Join(", ", languageConstraints)}.",
                languageConstraints);
        }
        var questionId = Guid.NewGuid();
        var testCases = RequiredArray(element, "test_cases")
            .Select(ParseTestCase)
            .ToList();
        var problemDescription = RequiredString(element, "problem_description_markdown");

        starterCode = prototypeSource.ApplyCanonicalFiles(starterCode, languageConstraints);
        ValidateTestCodeCoverage(title, languageConstraints, testCases);
        ValidateAdvancedTaskStructure(
            title,
            taskType,
            problemDescription,
            languageConstraints,
            starterCode,
            testCases);
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
            Difficulty = difficulty,
            VerificationMode = verificationMode,
            StarterPrototypeReference = PrototypeDefaults.TodoListReference,
            ProblemDescriptionMarkdown = problemDescription,
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(languageConstraints),
            StarterCodeJson = JsonDocumentSerializer.Serialize(starterCode),
            StarterFilesMetadataJson = JsonDocumentSerializer.Serialize(starterMetadata),
            VerificationMetadataJson = JsonDocumentSerializer.Serialize(
                ReadStringDictionary(element, "verification_metadata") ?? new Dictionary<string, string> { ["primary_view"] = verificationMode }),
            GradingConfigurationJson = JsonDocumentSerializer.Serialize(
                TaskAiUsageBenchmarkFactory.AddToConfiguration(
                    ReadStringDictionary(element, "grading_configuration"),
                    taskType,
                    difficulty)),
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
                    $"Generated task '{title}' test case '{testCase.Name}' is missing test code for language '{language}'. Regenerate the draft so the LLM provides complete tests.",
                    languageConstraints);
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

        ValidateDatabaseSqlTestSetup(title, taskType, starterCode, testCases);
    }

    private static void ValidateDatabaseSqlTestSetup(
        string title,
        string taskType,
        IReadOnlyDictionary<string, Dictionary<string, string>> starterCode,
        IReadOnlyCollection<TestCase> testCases)
    {
        if (!string.Equals(NormalizeTaskType(taskType), TaskTypes.DatabaseQuerySchema, StringComparison.Ordinal)
            || !starterCode.TryGetValue("sql", out var sqlStarterFiles))
        {
            return;
        }

        sqlStarterFiles.TryGetValue("seed.sql", out var seedSql);
        var normalizedSeedSql = seedSql ?? string.Empty;

        foreach (var testCase in testCases)
        {
            var testCodeByLanguage = JsonDocumentSerializer.Deserialize(testCase.TestCodeJson, new Dictionary<string, string>());
            if (!testCodeByLanguage.TryGetValue("sql", out var sqlTestCode)
                || string.IsNullOrWhiteSpace(sqlTestCode)
                || IsJestStyleTestCode(sqlTestCode)
                || !System.Text.RegularExpressions.Regex.IsMatch(sqlTestCode, @"\bselect\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                continue;
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(sqlTestCode, @"\b(?:sqlite_master|pragma_table_info|pragma\s+table_info)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                continue;
            }

            var mutatesRowsInTest = System.Text.RegularExpressions.Regex.IsMatch(
                sqlTestCode,
                @"\b(?:insert\s+into|update|delete\s+from)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var seedCreatesRows = System.Text.RegularExpressions.Regex.IsMatch(
                normalizedSeedSql,
                @"\binsert\s+into\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!mutatesRowsInTest && !seedCreatesRows)
            {
                throw new AiDraftGenerationException(
                    $"Generated database task '{title}' test case '{testCase.Name}' has a raw SELECT test without setup rows. Add INSERT/UPDATE/DELETE setup statements before the final SELECT or provide seed.sql rows that make the correct solution return data.");
            }

            var readsAuditLog = System.Text.RegularExpressions.Regex.IsMatch(
                sqlTestCode,
                @"\baudit_log\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var createsAuditRowsInTest = System.Text.RegularExpressions.Regex.IsMatch(
                sqlTestCode,
                @"\b(?:insert\s+into\s+(?:audit_log|todos)|update\s+todos|delete\s+from\s+todos)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (readsAuditLog && !createsAuditRowsInTest)
            {
                throw new AiDraftGenerationException(
                    $"Generated database task '{title}' test case '{testCase.Name}' reads audit_log but does not create audit rows after solution.sql is loaded. Add todo INSERT/UPDATE/DELETE setup statements before the final SELECT.");
            }
        }
    }

    private static bool IsJestStyleTestCode(string testCode)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            testCode,
            @"\b(?:test|it)\s*\(",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
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
