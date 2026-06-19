using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api;

public static class QuestionEndpoints
{
    private const int MaximumRegenerationAttempts = 3;

    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/admin/assessments/{assessmentId:guid}/questions", CreateQuestionAsync);
        api.MapPost("/admin/questions/{questionId:guid}/regenerate", RegenerateQuestionAsync);
        api.MapPut("/admin/questions/{questionId:guid}", UpdateQuestionAsync);
        api.MapDelete("/admin/questions/{questionId:guid}", DeleteQuestionAsync);
        api.MapGet("/admin/questions/{questionId:guid}/test-cases", ListTestCasesAsync);
        api.MapPost("/admin/questions/{questionId:guid}/test-cases", CreateTestCaseAsync);
        api.MapPut("/admin/test-cases/{testCaseId:guid}", UpdateTestCaseAsync);
        api.MapDelete("/admin/test-cases/{testCaseId:guid}", DeleteTestCaseAsync);
    }

    private static async Task<IResult> CreateQuestionAsync(
        Guid assessmentId,
        QuestionRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        if (!await dbContext.Assessments.AnyAsync(assessment => assessment.Id == assessmentId, cancellationToken))
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        var question = new Question
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessmentId,
            Title = request.Title,
            ProblemDescriptionMarkdown = request.ProblemDescriptionMarkdown,
            TaskType = NormalizeTaskType(request.TaskType),
            Difficulty = NormalizeDifficulty(request.Difficulty),
            VerificationMode = NormalizeVerificationMode(request.VerificationMode, request.TaskType),
            StarterPrototypeReference = PrototypeDefaults.TodoListReference,
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(request.LanguageConstraints),
            StarterCodeJson = JsonDocumentSerializer.Serialize(request.StarterCode),
            StarterFilesMetadataJson = JsonDocumentSerializer.Serialize(request.StarterFilesMetadata ?? new Dictionary<string, Dictionary<string, string>>()),
            VerificationMetadataJson = JsonDocumentSerializer.Serialize(request.VerificationMetadata ?? new Dictionary<string, string>()),
            GradingConfigurationJson = JsonDocumentSerializer.Serialize(request.GradingConfiguration ?? new Dictionary<string, string>()),
            AuthoringSource = NormalizeAuthoringSource(request.AuthoringSource),
            TraceabilityMetadataJson = JsonDocumentSerializer.Serialize(request.TraceabilityMetadata ?? new Dictionary<string, string>()),
            AdminNotes = request.AdminNotes,
            SortOrder = request.SortOrder,
            MaxScore = request.MaxScore
        };

        dbContext.Questions.Add(question);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(ToQuestionDto(question));
    }

    private static async Task<IResult> UpdateQuestionAsync(
        Guid questionId,
        QuestionRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var question = await dbContext.Questions.FindAsync([questionId], cancellationToken);
        if (question is null)
        {
            return ApiResults.Error("QUESTION_NOT_FOUND", "Question was not found.", StatusCodes.Status404NotFound);
        }

        question.Title = request.Title;
        question.ProblemDescriptionMarkdown = request.ProblemDescriptionMarkdown;
        question.TaskType = NormalizeTaskType(request.TaskType);
        question.Difficulty = NormalizeDifficulty(request.Difficulty);
        question.VerificationMode = NormalizeVerificationMode(request.VerificationMode, request.TaskType);
        question.StarterPrototypeReference = PrototypeDefaults.TodoListReference;
        question.LanguageConstraintsJson = JsonDocumentSerializer.Serialize(request.LanguageConstraints);
        question.StarterCodeJson = JsonDocumentSerializer.Serialize(request.StarterCode);
        question.StarterFilesMetadataJson = JsonDocumentSerializer.Serialize(request.StarterFilesMetadata ?? new Dictionary<string, Dictionary<string, string>>());
        question.VerificationMetadataJson = JsonDocumentSerializer.Serialize(request.VerificationMetadata ?? new Dictionary<string, string>());
        question.GradingConfigurationJson = JsonDocumentSerializer.Serialize(request.GradingConfiguration ?? new Dictionary<string, string>());
        question.AuthoringSource = NormalizeAuthoringSource(request.AuthoringSource);
        question.TraceabilityMetadataJson = JsonDocumentSerializer.Serialize(request.TraceabilityMetadata ?? new Dictionary<string, string>());
        question.AdminNotes = request.AdminNotes;
        question.SortOrder = request.SortOrder;
        question.MaxScore = request.MaxScore;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(ToQuestionDto(question));
    }

    private static async Task<IResult> RegenerateQuestionAsync(
        Guid questionId,
        GenerateQuestionDraftRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        AssessmentDraftGenerationService draftGenerationService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var question = await dbContext.Questions
            .Include(item => item.Assessment)
            .Include(item => item.TestCases)
            .FirstOrDefaultAsync(item => item.Id == questionId, cancellationToken);
        if (question is null)
        {
            return ApiResults.Error("QUESTION_NOT_FOUND", "Question was not found.", StatusCodes.Status404NotFound);
        }

        if (question.Assessment?.Status != AssessmentStatuses.Draft)
        {
            return ApiResults.Error(
                "DRAFT_REQUIRED",
                "Generated task drafts can only replace questions while the assessment is in draft status.",
                StatusCodes.Status409Conflict);
        }

        try
        {
            Question? draft = null;
            var avoidedDrafts = new List<(string Title, string Problem)>
            {
                (question.Title, question.ProblemDescriptionMarkdown)
            };

            for (var attempt = 1; attempt <= MaximumRegenerationAttempts; attempt += 1)
            {
                var regenerationRequest = request with
                {
                    ProblemDescriptionMarkdown = BuildRegenerationGuidance(avoidedDrafts, attempt)
                };
                var candidate = await draftGenerationService.GenerateQuestionDraftAsync(
                    question.AssessmentId,
                    regenerationRequest,
                    question.Assessment.SharedPrototypeReference,
                    question.SortOrder,
                    cancellationToken);

                if (IsMateriallyDifferent(candidate, avoidedDrafts))
                {
                    draft = candidate;
                    break;
                }

                avoidedDrafts.Add((candidate.Title, candidate.ProblemDescriptionMarkdown));
            }

            if (draft is null)
            {
                throw new AiDraftGenerationException(
                    "The AI provider repeatedly returned the same task. Please try regeneration again.");
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            question.Title = draft.Title;
            question.TaskType = draft.TaskType;
            question.Difficulty = draft.Difficulty;
            question.VerificationMode = draft.VerificationMode;
            question.StarterPrototypeReference = draft.StarterPrototypeReference;
            question.ProblemDescriptionMarkdown = draft.ProblemDescriptionMarkdown;
            question.LanguageConstraintsJson = draft.LanguageConstraintsJson;
            question.StarterCodeJson = draft.StarterCodeJson;
            question.StarterFilesMetadataJson = draft.StarterFilesMetadataJson;
            question.VerificationMetadataJson = draft.VerificationMetadataJson;
            question.GradingConfigurationJson = draft.GradingConfigurationJson;
            question.AuthoringSource = AuthoringSources.LlmGenerated;
            question.TraceabilityMetadataJson = draft.TraceabilityMetadataJson;
            question.AdminNotes = draft.AdminNotes;
            question.MaxScore = draft.MaxScore;

            var existingTestCases = question.TestCases.ToList();
            dbContext.TestCases.RemoveRange(existingTestCases);
            question.TestCases.Clear();

            // Persist removals before inserting regenerated children. This avoids
            // provider-specific command ordering failures when a tracked question's
            // complete child collection is replaced in a single SaveChanges call.
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var testCase in draft.TestCases)
            {
                testCase.Id = Guid.NewGuid();
                testCase.QuestionId = question.Id;
                question.TestCases.Add(testCase);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ApiResults.Success(ToQuestionDto(question));
        }
        catch (AiProviderUnavailableException exception)
        {
            return ApiResults.Error("AI_PROVIDER_UNAVAILABLE", exception.Message, StatusCodes.Status503ServiceUnavailable);
        }
        catch (AiDraftGenerationException exception)
        {
            return ApiResults.Error("AI_DRAFT_GENERATION_FAILED", exception.Message, StatusCodes.Status502BadGateway);
        }
    }

    internal static bool IsMateriallyDifferent(
        Question candidate,
        IReadOnlyCollection<(string Title, string Problem)> avoidedDrafts)
    {
        var candidateTitle = NormalizeComparisonText(candidate.Title);
        var candidateProblem = NormalizeComparisonText(candidate.ProblemDescriptionMarkdown);

        return avoidedDrafts.All(avoided =>
            candidateTitle != NormalizeComparisonText(avoided.Title)
            || candidateProblem != NormalizeComparisonText(avoided.Problem));
    }

    private static string BuildRegenerationGuidance(
        IReadOnlyCollection<(string Title, string Problem)> avoidedDrafts,
        int attempt)
    {
        var avoidedContent = avoidedDrafts.SelectMany((avoided, index) => new[]
        {
            $"Avoided task {index + 1} title: {avoided.Title}",
            $"Avoided task {index + 1} problem: {avoided.Problem}"
        });

        return string.Join("\n",
        [
            "Create a materially different task from every avoided task listed below.",
            "Use a different scenario, feature set, starter implementation, expected behavior, and tests.",
            "Do not reuse or lightly reword an avoided title or problem description.",
            "Keep only the requested task type, difficulty, supported languages, and shared prototype constraints.",
            $"Variation attempt: {attempt}-{Guid.NewGuid():N}",
            .. avoidedContent
        ]);
    }

    private static string NormalizeComparisonText(string value)
    {
        return string.Join(
            " ",
            value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Trim()
            .ToLowerInvariant();
    }

    private static async Task<IResult> DeleteQuestionAsync(
        Guid questionId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var question = await dbContext.Questions.FindAsync([questionId], cancellationToken);
        if (question is null)
        {
            return ApiResults.Error("QUESTION_NOT_FOUND", "Question was not found.", StatusCodes.Status404NotFound);
        }

        dbContext.Questions.Remove(question);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { question_id = questionId, deleted = true });
    }

    private static async Task<IResult> ListTestCasesAsync(
        Guid questionId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var testCases = await dbContext.TestCases
            .Where(testCase => testCase.QuestionId == questionId)
            .OrderBy(testCase => testCase.Name)
            .Select(testCase => new
            {
                test_case_id = testCase.Id,
                testCase.Name,
                testCase.Visibility,
                test_code = JsonDocumentSerializer.Deserialize(testCase.TestCodeJson, new Dictionary<string, string>()),
                authoring_source = testCase.AuthoringSource,
                public_metadata = JsonDocumentSerializer.Deserialize(testCase.PublicMetadataJson, new Dictionary<string, string>()),
                admin_metadata = JsonDocumentSerializer.Deserialize(testCase.AdminMetadataJson, new Dictionary<string, string>()),
                traceability_metadata = JsonDocumentSerializer.Deserialize(testCase.TraceabilityMetadataJson, new Dictionary<string, string>())
            })
            .ToListAsync(cancellationToken);

        return ApiResults.Success(testCases);
    }

    private static async Task<IResult> CreateTestCaseAsync(
        Guid questionId,
        TestCaseRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        if (!await dbContext.Questions.AnyAsync(question => question.Id == questionId, cancellationToken))
        {
            return ApiResults.Error("QUESTION_NOT_FOUND", "Question was not found.", StatusCodes.Status404NotFound);
        }

        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            QuestionId = questionId,
            Name = request.Name,
            Visibility = NormalizeVisibility(request.Visibility),
            TestCodeJson = JsonDocumentSerializer.Serialize(NormalizeTestCode(request.TestCode)),
            AuthoringSource = NormalizeAuthoringSource(request.AuthoringSource),
            PublicMetadataJson = JsonDocumentSerializer.Serialize(request.PublicMetadata ?? new Dictionary<string, string>()),
            AdminMetadataJson = JsonDocumentSerializer.Serialize(request.AdminMetadata ?? new Dictionary<string, string>()),
            TraceabilityMetadataJson = JsonDocumentSerializer.Serialize(request.TraceabilityMetadata ?? new Dictionary<string, string>())
        };

        dbContext.TestCases.Add(testCase);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { test_case_id = testCase.Id });
    }

    private static async Task<IResult> UpdateTestCaseAsync(
        Guid testCaseId,
        TestCaseRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var testCase = await dbContext.TestCases.FindAsync([testCaseId], cancellationToken);
        if (testCase is null)
        {
            return ApiResults.Error("NOT_FOUND", "Test case was not found.", StatusCodes.Status404NotFound);
        }

        testCase.Name = request.Name;
        testCase.Visibility = NormalizeVisibility(request.Visibility);
        testCase.TestCodeJson = JsonDocumentSerializer.Serialize(NormalizeTestCode(request.TestCode));
        testCase.AuthoringSource = NormalizeAuthoringSource(request.AuthoringSource);
        testCase.PublicMetadataJson = JsonDocumentSerializer.Serialize(request.PublicMetadata ?? new Dictionary<string, string>());
        testCase.AdminMetadataJson = JsonDocumentSerializer.Serialize(request.AdminMetadata ?? new Dictionary<string, string>());
        testCase.TraceabilityMetadataJson = JsonDocumentSerializer.Serialize(request.TraceabilityMetadata ?? new Dictionary<string, string>());

        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { test_case_id = testCase.Id });
    }

    private static async Task<IResult> DeleteTestCaseAsync(
        Guid testCaseId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var testCase = await dbContext.TestCases.FindAsync([testCaseId], cancellationToken);
        if (testCase is null)
        {
            return ApiResults.Error("NOT_FOUND", "Test case was not found.", StatusCodes.Status404NotFound);
        }

        dbContext.TestCases.Remove(testCase);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { test_case_id = testCaseId, deleted = true });
    }

    private static object ToQuestionDto(Question question)
    {
        return new
        {
            question_id = question.Id,
            question.Title,
            task_type = NormalizeTaskType(question.TaskType),
            difficulty = question.Difficulty,
            verification_mode = NormalizeVerificationMode(question.VerificationMode, question.TaskType),
            starter_prototype_reference = question.StarterPrototypeReference,
            problem_description_markdown = question.ProblemDescriptionMarkdown,
            language_constraints = JsonDocumentSerializer.Deserialize(question.LanguageConstraintsJson, Array.Empty<string>()),
            starter_code = JsonDocumentSerializer.DeserializeStarterCode(question.StarterCodeJson),
            starter_files_metadata = JsonDocumentSerializer.Deserialize(question.StarterFilesMetadataJson, new Dictionary<string, Dictionary<string, string>>()),
            verification_metadata = JsonDocumentSerializer.Deserialize(question.VerificationMetadataJson, new Dictionary<string, string>()),
            grading_configuration = JsonDocumentSerializer.Deserialize(question.GradingConfigurationJson, new Dictionary<string, string>()),
            authoring_source = question.AuthoringSource,
            traceability_metadata = JsonDocumentSerializer.Deserialize(question.TraceabilityMetadataJson, new Dictionary<string, string>()),
            admin_notes = question.AdminNotes,
            sort_order = question.SortOrder,
            max_score = question.MaxScore,
            admin_test_cases = question.TestCases
                .OrderBy(testCase => testCase.Name)
                .Select(testCase => new
                {
                    test_case_id = testCase.Id,
                    testCase.Name,
                    testCase.Visibility,
                    test_code = JsonDocumentSerializer.Deserialize(testCase.TestCodeJson, new Dictionary<string, string>()),
                    authoring_source = testCase.AuthoringSource,
                    public_metadata = JsonDocumentSerializer.Deserialize(testCase.PublicMetadataJson, new Dictionary<string, string>()),
                    admin_metadata = JsonDocumentSerializer.Deserialize(testCase.AdminMetadataJson, new Dictionary<string, string>()),
                    traceability_metadata = JsonDocumentSerializer.Deserialize(testCase.TraceabilityMetadataJson, new Dictionary<string, string>())
                })
        };
    }

    private static string NormalizeVisibility(string visibility)
    {
        return visibility == TestCaseVisibilities.Hidden ? TestCaseVisibilities.Hidden : TestCaseVisibilities.Public;
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

    private static string NormalizeVerificationMode(string? verificationMode, string? taskType)
    {
        if (verificationMode is VerificationModes.BrowserUiPreview
            or VerificationModes.ApiResponseCheck
            or VerificationModes.DatabaseResultCheck
            or VerificationModes.AutomatedTest
            or VerificationModes.RegressionTest)
        {
            return verificationMode;
        }

        return NormalizeTaskType(taskType) switch
        {
            TaskTypes.FrontendUiExtension => VerificationModes.BrowserUiPreview,
            TaskTypes.RestApiDevelopment => VerificationModes.ApiResponseCheck,
            TaskTypes.DatabaseQuerySchema => VerificationModes.DatabaseResultCheck,
            TaskTypes.BugFix => VerificationModes.RegressionTest,
            _ => VerificationModes.AutomatedTest
        };
    }

    private static string NormalizeAuthoringSource(string? authoringSource)
    {
        return authoringSource is AuthoringSources.LlmGenerated or AuthoringSources.AdminEdited
            ? authoringSource
            : AuthoringSources.Manual;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static Dictionary<string, string> NormalizeTestCode(Dictionary<string, string>? testCode)
    {
        return testCode is null
            ? new Dictionary<string, string>()
            : testCode.ToDictionary(item => item.Key.ToLowerInvariant(), item => item.Value);
    }
}
