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
        api.MapPost("/sessions/initiate", InitiateAsync);
        api.MapGet("/sessions/{sessionId:guid}", GetAsync);
        api.MapPost("/sessions/{sessionId:guid}/complete", CompleteAsync);
    }

    private static async Task<IResult> InitiateAsync(
        InitiateSessionRequest request,
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

        var assessment = await dbContext.Assessments
            .Include(item => item.Questions)
            .FirstOrDefaultAsync(item => item.Id == request.AssessmentId, cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        if (assessment.Status != AssessmentStatuses.Active)
        {
            return ApiResults.Error("ASSESSMENT_CLOSED", "Assessment is not open for new sessions.", StatusCodes.Status409Conflict);
        }

        var now = DateTimeOffset.UtcNow;
        var session = await dbContext.AssessmentSessions
            .Include(item => item.WorkspaceStates)
            .FirstOrDefaultAsync(
                item => item.AssessmentId == request.AssessmentId
                        && item.UserId == user!.Id
                        && item.Status == SessionStatuses.Active
                        && item.ExpiresAt > now,
                cancellationToken);

        if (session is null)
        {
            session = new AssessmentSession
            {
                Id = Guid.NewGuid(),
                AssessmentId = assessment.Id,
                UserId = user!.Id,
                Status = SessionStatuses.Active,
                StartedAt = now,
                ExpiresAt = now.AddMinutes(assessment.DurationMinutes)
            };
            dbContext.AssessmentSessions.Add(session);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await EnsureWorkspaceAsync(dbContext, session, assessment, now, cancellationToken);

        return ApiResults.Success(ToSessionDto(session, now));
    }

    private static async Task<IResult> GetAsync(
        Guid sessionId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SessionClock sessionClock,
        CancellationToken cancellationToken)
    {
        var (user, error) = await currentUserAccessor.RequireUserAsync(httpContext, dbContext, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var session = await dbContext.AssessmentSessions.FirstOrDefaultAsync(item => item.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("SESSION_NOT_FOUND", "Session was not found.", StatusCodes.Status404NotFound);
        }

        if (user!.Role == UserRoles.Student && session.UserId != user.Id)
        {
            return ApiResults.Error("FORBIDDEN", "The current user cannot access this session.", StatusCodes.Status403Forbidden);
        }

        if (session.Status == SessionStatuses.Active && sessionClock.GetEffectiveStatus(session) == SessionStatuses.Expired)
        {
            session.Status = SessionStatuses.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ApiResults.Success(ToSessionDto(session, DateTimeOffset.UtcNow));
    }

    private static async Task<IResult> CompleteAsync(
        Guid sessionId,
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

        var session = await dbContext.AssessmentSessions.FirstOrDefaultAsync(item => item.Id == sessionId && item.UserId == user!.Id, cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("SESSION_NOT_FOUND", "Session was not found.", StatusCodes.Status404NotFound);
        }

        session.Status = SessionStatuses.Closed;
        session.CompletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(ToSessionDto(session, DateTimeOffset.UtcNow));
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

        foreach (var question in assessment.Questions.Where(question => !existingQuestionIds.Contains(question.Id)))
        {
            var starterCode = JsonDocumentSerializer.Deserialize(question.StarterCodeJson, new Dictionary<string, string>());
            var language = starterCode.ContainsKey("python") ? "python" : starterCode.Keys.FirstOrDefault() ?? "python";
            var activeFile = language == "javascript" ? "main.js" : "main.py";
            dbContext.WorkspaceQuestionStates.Add(new WorkspaceQuestionState
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                QuestionId = question.Id,
                SelectedLanguage = language,
                ActiveFile = activeFile,
                FilesJson = JsonDocumentSerializer.Serialize(new Dictionary<string, WorkspaceFileDto>
                {
                    [activeFile] = new WorkspaceFileDto(language, starterCode.GetValueOrDefault(language, string.Empty))
                }),
                LastSavedAt = now,
                Version = 1
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static object ToSessionDto(AssessmentSession session, DateTimeOffset now)
    {
        return new
        {
            session_id = session.Id,
            assessment_id = session.AssessmentId,
            session_status = session.Status,
            started_at = session.StartedAt,
            expires_at = session.ExpiresAt,
            server_time = now
        };
    }
}
