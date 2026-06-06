using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api;

public static class AssessmentEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/admin/assessments", ListAdminAsync);
        api.MapPost("/admin/assessments", CreateAsync);
        api.MapPost("/admin/assessments/generate", GenerateAsync);
        api.MapPost("/admin/assessments/{assessmentId:guid}/questions/generate", GenerateQuestionDraftAsync);
        api.MapGet("/admin/assessments/{assessmentId:guid}", GetAdminAsync);
        api.MapPut("/admin/assessments/{assessmentId:guid}", UpdateAsync);
        api.MapPost("/admin/assessments/{assessmentId:guid}/archive", ArchiveAsync);
        api.MapDelete("/admin/assessments/{assessmentId:guid}", DeleteAsync);
        api.MapGet("/assessments/{assessmentId:guid}/context", ContextAsync);
    }

    private static async Task<IResult> ListAdminAsync(
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        AssessmentProjectionService projectionService,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var assessments = await dbContext.Assessments
            .Include(assessment => assessment.Questions)
            .OrderByDescending(assessment => assessment.CreatedAt)
            .ToListAsync(cancellationToken);

        return ApiResults.Success(assessments.Select(projectionService.ToAdminAssessment));
    }

    private static async Task<IResult> CreateAsync(
        AssessmentRequest request,
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

        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            DurationMinutes = request.DurationMinutes,
            Status = NormalizeAssessmentStatus(request.Status),
            AiEnabled = request.AiEnabled,
            SharedPrototypeReference = NormalizeOptionalText(request.SharedPrototypeReference),
            SharedPrototypeVersion = NormalizeOptionalText(request.SharedPrototypeVersion),
            SharedPrototypeMetadataJson = JsonDocumentSerializer.Serialize(request.SharedPrototypeMetadata ?? new Dictionary<string, string>()),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Assessments.Add(assessment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { assessment_id = assessment.Id });
    }

    private static async Task<IResult> GenerateQuestionDraftAsync(
        Guid assessmentId,
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

        var assessment = await dbContext.Assessments
            .Include(item => item.Questions)
            .ThenInclude(question => question.TestCases)
            .FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        if (assessment.Status != AssessmentStatuses.Draft)
        {
            return ApiResults.Error(
                "DRAFT_REQUIRED",
                "Generated task drafts can only be added while the assessment is in draft status.",
                StatusCodes.Status409Conflict);
        }

        try
        {
            var sortOrder = assessment.Questions.Count == 0
                ? 1
                : assessment.Questions.Max(question => question.SortOrder) + 1;
            var draft = await draftGenerationService.GenerateQuestionDraftAsync(
                assessment.Id,
                request,
                assessment.SharedPrototypeReference,
                sortOrder,
                cancellationToken);

            dbContext.Questions.Add(draft);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ApiResults.Success(ToQuestionDto(draft));
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

    private static async Task<IResult> GenerateAsync(
        AssessmentRequest request,
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

        var assessmentId = Guid.NewGuid();
        try
        {
            var questions = await draftGenerationService.GenerateAssessmentDraftAsync(
                assessmentId,
                request,
                cancellationToken);
            var assessment = new Assessment
            {
                Id = assessmentId,
                Title = request.Title,
                Description = request.Description,
                DurationMinutes = request.DurationMinutes,
                Status = AssessmentStatuses.Draft,
                AiEnabled = request.AiEnabled,
                SharedPrototypeReference = NormalizeOptionalText(request.SharedPrototypeReference),
                SharedPrototypeVersion = NormalizeOptionalText(request.SharedPrototypeVersion),
                SharedPrototypeMetadataJson = JsonDocumentSerializer.Serialize(request.SharedPrototypeMetadata ?? new Dictionary<string, string>()),
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Assessments.Add(assessment);
            assessment.Questions.AddRange(questions);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ApiResults.Success(new { assessment_id = assessment.Id });
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

    private static async Task<IResult> GetAdminAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        AssessmentProjectionService projectionService,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var assessment = await dbContext.Assessments
            .Include(item => item.Questions.OrderBy(question => question.SortOrder))
            .ThenInclude(question => question.TestCases)
            .FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken);

        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        return ApiResults.Success(projectionService.ToAdminAssessmentDetail(assessment));
    }

    private static async Task<IResult> UpdateAsync(
        Guid assessmentId,
        AssessmentRequest request,
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

        var assessment = await dbContext.Assessments.FindAsync([assessmentId], cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        assessment.Title = request.Title;
        assessment.Description = request.Description;
        assessment.DurationMinutes = request.DurationMinutes;
        assessment.Status = NormalizeAssessmentStatus(request.Status);
        assessment.AiEnabled = request.AiEnabled;
        assessment.SharedPrototypeReference = NormalizeOptionalText(request.SharedPrototypeReference);
        assessment.SharedPrototypeVersion = NormalizeOptionalText(request.SharedPrototypeVersion);
        assessment.SharedPrototypeMetadataJson = JsonDocumentSerializer.Serialize(request.SharedPrototypeMetadata ?? new Dictionary<string, string>());
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { assessment_id = assessment.Id });
    }

    private static async Task<IResult> ArchiveAsync(
        Guid assessmentId,
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

        var assessment = await dbContext.Assessments.FindAsync([assessmentId], cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        assessment.Status = AssessmentStatuses.Archived;
        assessment.ArchivedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { assessment_id = assessment.Id, assessment.Status });
    }

    private static async Task<IResult> DeleteAsync(
        Guid assessmentId,
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

        var assessment = await dbContext.Assessments.FindAsync([assessmentId], cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        assessment.Status = AssessmentStatuses.Archived;
        assessment.ArchivedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { assessment_id = assessment.Id, deleted = false, archived = true });
    }

    private static async Task<IResult> ContextAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        AssessmentProjectionService projectionService,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (user, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Student, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var assessment = await dbContext.Assessments
            .Include(item => item.Questions.OrderBy(question => question.SortOrder))
            .FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        var session = await dbContext.AssessmentSessions.FirstOrDefaultAsync(
            item => item.AssessmentId == assessmentId
                    && item.UserId == user!.Id
                    && item.Status == SessionStatuses.Active
                    && item.ExpiresAt > DateTimeOffset.UtcNow,
            cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("ATTEMPT_NOT_FOUND", "Active assessment attempt was not found.", StatusCodes.Status404NotFound);
        }

        return ApiResults.Success(projectionService.ToStudentContext(assessment, session));
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

    private static string NormalizeAssessmentStatus(string status)
    {
        return status is AssessmentStatuses.Draft or AssessmentStatuses.Active or AssessmentStatuses.Closed or AssessmentStatuses.Archived
            ? status
            : AssessmentStatuses.Draft;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static object ToQuestionDto(Question question)
    {
        return new
        {
            question_id = question.Id,
            question.Title,
            task_type = NormalizeTaskType(question.TaskType),
            difficulty = question.Difficulty,
            verification_mode = question.VerificationMode,
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
}
