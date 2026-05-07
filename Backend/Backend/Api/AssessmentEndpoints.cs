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
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

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
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            DurationMinutes = request.DurationMinutes,
            Status = NormalizeAssessmentStatus(request.Status),
            AiEnabled = request.AiEnabled,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Assessments.Add(assessment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { assessment_id = assessment.Id });
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
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

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
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { assessment_id = assessment.Id });
    }

    private static async Task<IResult> ArchiveAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

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
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

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
        CancellationToken cancellationToken)
    {
        var (user, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Student, cancellationToken);
        if (error is not null)
        {
            return error;
        }

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

    private static string NormalizeAssessmentStatus(string status)
    {
        return status is AssessmentStatuses.Draft or AssessmentStatuses.Active or AssessmentStatuses.Closed or AssessmentStatuses.Archived
            ? status
            : AssessmentStatuses.Draft;
    }
}
