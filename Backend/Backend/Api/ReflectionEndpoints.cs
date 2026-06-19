using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api;

public static class ReflectionEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/assessments/{assessmentId:guid}/reflection", GetAsync);
        api.MapPut("/assessments/{assessmentId:guid}/reflection", SaveAsync);
        api.MapPost("/assessments/{assessmentId:guid}/reflection/submit", SubmitAsync);
    }

    private static async Task<IResult> GetAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        AiUsageGradingService gradingService,
        CancellationToken cancellationToken)
    {
        var (session, error) = await RequireSubmittedAiSessionAsync(
            assessmentId,
            httpContext,
            dbContext,
            currentUserAccessor,
            cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await FinalizeExpiredAsync(session!, gradingService, dbContext, cancellationToken);
        return ApiResults.Success(ToDto(session!));
    }

    private static async Task<IResult> SaveAsync(
        Guid assessmentId,
        ReflectionRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        AiUsageGradingService gradingService,
        CancellationToken cancellationToken)
    {
        var (session, error) = await RequireSubmittedAiSessionAsync(
            assessmentId,
            httpContext,
            dbContext,
            currentUserAccessor,
            cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (await FinalizeExpiredAsync(session!, gradingService, dbContext, cancellationToken))
        {
            return ApiResults.Error("REFLECTION_CLOSED", "The reflection deadline has passed.", StatusCodes.Status409Conflict);
        }

        if (session!.ReflectionSubmittedAt.HasValue)
        {
            return ApiResults.Error("REFLECTION_CLOSED", "The reflection has already been submitted.", StatusCodes.Status409Conflict);
        }

        var wordCount = AiUsageGradingService.CountWords(request.ReflectionText);
        if (wordCount > 100)
        {
            return ApiResults.Error("REFLECTION_TOO_LONG", "Reflection must contain no more than 100 words.", StatusCodes.Status400BadRequest);
        }

        session.ReflectionText = request.ReflectionText.Trim();
        session.ReflectionWordCount = wordCount;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(ToDto(session));
    }

    private static async Task<IResult> SubmitAsync(
        Guid assessmentId,
        ReflectionRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        AiUsageGradingService gradingService,
        CancellationToken cancellationToken)
    {
        var (session, error) = await RequireSubmittedAiSessionAsync(
            assessmentId,
            httpContext,
            dbContext,
            currentUserAccessor,
            cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (await FinalizeExpiredAsync(session!, gradingService, dbContext, cancellationToken))
        {
            return ApiResults.Success(ToDto(session!));
        }

        if (session!.ReflectionSubmittedAt.HasValue)
        {
            return ApiResults.Success(ToDto(session));
        }

        var reflection = request.ReflectionText.Trim();
        var wordCount = AiUsageGradingService.CountWords(reflection);
        if (wordCount == 0)
        {
            return ApiResults.Error("REFLECTION_REQUIRED", "Reflection is required before submission.", StatusCodes.Status400BadRequest);
        }

        if (wordCount > 100)
        {
            return ApiResults.Error("REFLECTION_TOO_LONG", "Reflection must contain no more than 100 words.", StatusCodes.Status400BadRequest);
        }

        session.ReflectionText = reflection;
        session.ReflectionWordCount = wordCount;
        session.ReflectionSubmittedAt = DateTimeOffset.UtcNow;
        session.ReflectionSubmissionReason = "student_submit";
        await dbContext.SaveChangesAsync(cancellationToken);
        await gradingService.GradeAsync(session, cancellationToken);
        return ApiResults.Success(ToDto(session));
    }

    internal static async Task<bool> FinalizeExpiredAsync(
        AssessmentSession session,
        AiUsageGradingService gradingService,
        OjSharpDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (session.ReflectionSubmittedAt.HasValue
            || !session.ReflectionDeadline.HasValue
            || session.ReflectionDeadline.Value > DateTimeOffset.UtcNow)
        {
            return false;
        }

        session.ReflectionSubmittedAt = session.ReflectionDeadline;
        session.ReflectionSubmissionReason = "timeout";
        session.ReflectionWordCount = AiUsageGradingService.CountWords(session.ReflectionText);
        await dbContext.SaveChangesAsync(cancellationToken);
        await gradingService.GradeAsync(session, cancellationToken);
        return true;
    }

    private static async Task<(AssessmentSession? Session, IResult? Error)> RequireSubmittedAiSessionAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (user, authError) = await currentUserAccessor.RequireRoleAsync(
            httpContext,
            dbContext,
            UserRoles.Student,
            cancellationToken);
        if (authError is not null)
        {
            return (null, authError);
        }

        var sessions = await dbContext.AssessmentSessions
            .Include(item => item.Assessment)
            .Where(item => item.AssessmentId == assessmentId && item.UserId == user!.Id)
            .ToListAsync(cancellationToken);
        var session = sessions.OrderByDescending(item => item.StartedAt).FirstOrDefault();
        if (session is null)
        {
            return (null, ApiResults.Error("ATTEMPT_NOT_FOUND", "Assessment attempt was not found.", StatusCodes.Status404NotFound));
        }

        if (session.Assessment?.AiEnabled != true)
        {
            return (null, ApiResults.Error("REFLECTION_NOT_REQUIRED", "This assessment does not require an AI reflection.", StatusCodes.Status409Conflict));
        }

        if (session.Status != SessionStatuses.Submitted || !session.ReflectionDeadline.HasValue)
        {
            return (null, ApiResults.Error("REFLECTION_NOT_READY", "Submit the assessment before completing the reflection.", StatusCodes.Status409Conflict));
        }

        return (session, null);
    }

    private static object ToDto(AssessmentSession session)
    {
        return new
        {
            assessment_id = session.AssessmentId,
            reflection_text = session.ReflectionText,
            word_count = session.ReflectionWordCount,
            reflection_deadline = session.ReflectionDeadline,
            reflection_submitted_at = session.ReflectionSubmittedAt,
            reflection_submission_reason = session.ReflectionSubmissionReason,
            grading_status = session.AiGradingStatus,
            ai_usage_score = session.AiUsageScore,
            grading_summary = session.AiGradingSummary
        };
    }
}
