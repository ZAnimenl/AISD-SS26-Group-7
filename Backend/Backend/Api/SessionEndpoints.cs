using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api;

public static class SessionEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/assessments/{assessmentId:guid}/attempts/start", StartAttemptAsync);
        api.MapGet("/assessments/{assessmentId:guid}/attempt", GetActiveAttemptAsync);
    }

    private static async Task<IResult> StartAttemptAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        return await StartAttemptForAssessmentAsync(
            assessmentId,
            httpContext,
            dbContext,
            currentUserAccessor,
            cancellationToken);
    }

    private static async Task<IResult> StartAttemptForAssessmentAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (user, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Student, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var student = user!;
        await using var attemptLock = await DatabaseAdvisoryLocks.AcquireSessionLockAsync(
            dbContext,
            DatabaseAdvisoryLocks.GetAssessmentAttemptKey(assessmentId, student.Id),
            cancellationToken);

        var assessment = await dbContext.Assessments
            .Include(item => item.Questions)
            .FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        if (assessment.Status != AssessmentStatuses.Active)
        {
            return ApiResults.Error("ASSESSMENT_CLOSED", "Assessment is not open for new attempts.", StatusCodes.Status409Conflict);
        }

        var now = DateTimeOffset.UtcNow;
        if (!AssessmentPolicy.HasAssessmentStarted(assessment, now))
        {
            return ApiResults.Error(
                "ASSESSMENT_NOT_STARTED",
                $"This assessment opens at {assessment.StartsAt:O}.",
                StatusCodes.Status409Conflict);
        }

        var studentSessions = dbContext.AssessmentSessions
            .Where(item => item.AssessmentId == assessmentId
                           && item.UserId == student.Id);
        var studentActiveSessions = studentSessions
            .Where(item => item.Status == SessionStatuses.Active);
        var expiredSessions = await SessionQueries.ToExpiredListAsync(
            studentActiveSessions,
            dbContext,
            now,
            cancellationToken);
        foreach (var expiredSession in expiredSessions)
        {
            expiredSession.Status = SessionStatuses.Expired;
        }

        if (expiredSessions.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var sessionStatuses = await studentSessions
            .Select(item => item.Status)
            .ToListAsync(cancellationToken);
        var hasExpiredAttempt = HasExpiredAttempt(sessionStatuses);
        if (hasExpiredAttempt)
        {
            return ApiResults.Error(
                "ATTEMPT_EXPIRED",
                "This assessment attempt has expired and cannot be started again.",
                StatusCodes.Status409Conflict);
        }

        var session = await SessionQueries.FirstUnexpiredAsync(
            studentActiveSessions,
            dbContext,
            now,
            cancellationToken);

        if (session is null)
        {
            session = new AssessmentSession
            {
                Id = Guid.NewGuid(),
                AssessmentId = assessment.Id,
                UserId = student.Id,
                Status = SessionStatuses.Active,
                StartedAt = now,
                ExpiresAt = now.AddMinutes(assessment.DurationMinutes)
            };
            dbContext.AssessmentSessions.Add(session);
            dbContext.WorkspaceQuestionStates.AddRange(CreateMissingWorkspaceStates(
                session.Id,
                assessment.Questions,
                new HashSet<Guid>(),
                now));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await EnsureWorkspaceAsync(dbContext, session, assessment, now, cancellationToken);
        }

        return ApiResults.Success(ToAttemptDto(session, now));
    }

    private static async Task<IResult> GetActiveAttemptAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SessionClock sessionClock,
        CancellationToken cancellationToken)
    {
        var (user, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Student, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var session = await dbContext.AssessmentSessions
            .FirstOrDefaultAsync(
                item => item.AssessmentId == assessmentId && item.UserId == user!.Id && item.Status == SessionStatuses.Active,
                cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("ATTEMPT_NOT_FOUND", "Active assessment attempt was not found.", StatusCodes.Status404NotFound);
        }

        if (sessionClock.GetEffectiveStatus(session) == SessionStatuses.Expired)
        {
            session.Status = SessionStatuses.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ApiResults.Success(ToAttemptDto(session, DateTimeOffset.UtcNow));
    }

    private static async Task EnsureWorkspaceAsync(
        OjSharpDbContext dbContext,
        AssessmentSession session,
        Assessment assessment,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existingQuestionIds = await dbContext.WorkspaceQuestionStates
            .Where(state => state.SessionId == session.Id)
            .Select(state => state.QuestionId)
            .ToListAsync(cancellationToken);

        var missingWorkspaceStates = CreateMissingWorkspaceStates(
            session.Id,
            assessment.Questions,
            existingQuestionIds.ToHashSet(),
            now);
        if (missingWorkspaceStates.Count == 0)
        {
            return;
        }

        dbContext.WorkspaceQuestionStates.AddRange(missingWorkspaceStates);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    internal static IReadOnlyList<WorkspaceQuestionState> CreateMissingWorkspaceStates(
        Guid sessionId,
        IEnumerable<Question> questions,
        IReadOnlySet<Guid> existingQuestionIds,
        DateTimeOffset now)
    {
        var workspaceStates = new List<WorkspaceQuestionState>();
        foreach (var question in questions
                     .Where(question => !existingQuestionIds.Contains(question.Id))
                     .OrderBy(question => question.SortOrder)
                     .ThenBy(question => question.Id))
        {
            workspaceStates.Add(WorkspaceStateFactory.Create(sessionId, question, now));
        }

        return workspaceStates;
    }

    internal static bool HasExpiredAttempt(IEnumerable<string> sessionStatuses)
    {
        return sessionStatuses.Any(status => status == SessionStatuses.Expired);
    }

    private static object ToAttemptDto(AssessmentSession session, DateTimeOffset now)
    {
        return new
        {
            attempt_id = session.Id,
            assessment_id = session.AssessmentId,
            attempt_status = session.Status,
            started_at = session.StartedAt,
            expires_at = session.ExpiresAt,
            server_time = now
        };
    }
}
