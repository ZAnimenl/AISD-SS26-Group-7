using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api;

public static class WorkspaceEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/assessments/{assessmentId:guid}/workspace", GetByAssessmentAsync);
        api.MapPut("/assessments/{assessmentId:guid}/workspace", SaveByAssessmentAsync);
    }

    private static async Task<IResult> GetByAssessmentAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        WorkspaceProjectionService projectionService,
        CancellationToken cancellationToken)
    {
        var (session, error) = await ResolveActiveSessionAsync(
            assessmentId,
            httpContext,
            dbContext,
            currentUserAccessor,
            cancellationToken);
        if (error is not null)
        {
            return error;
        }

        return await GetForSessionAsync(session!.Id, dbContext, projectionService, cancellationToken);
    }

    private static async Task<IResult> SaveByAssessmentAsync(
        Guid assessmentId,
        WorkspaceUpdateRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SessionClock sessionClock,
        WorkspaceProjectionService projectionService,
        CancellationToken cancellationToken)
    {
        var (session, error) = await ResolveActiveSessionAsync(
            assessmentId,
            httpContext,
            dbContext,
            currentUserAccessor,
            cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (sessionClock.IsClosed(session!))
        {
            return ApiResults.Error("ATTEMPT_EXPIRED", "The assessment attempt has expired.", StatusCodes.Status409Conflict);
        }

        return await SaveForSessionAsync(session!.Id, session.AssessmentId, request, dbContext, projectionService, cancellationToken);
    }

    private static async Task<IResult> GetForSessionAsync(
        Guid sessionId,
        OjSharpDbContext dbContext,
        WorkspaceProjectionService projectionService,
        CancellationToken cancellationToken)
    {
        var states = await dbContext.WorkspaceQuestionStates
            .Where(state => state.SessionId == sessionId)
            .OrderBy(state => state.QuestionId)
            .ToListAsync(cancellationToken);
        return ApiResults.Success(projectionService.ToWorkspace(sessionId, states));
    }

    private static async Task<IResult> SaveForSessionAsync(
        Guid sessionId,
        Guid assessmentId,
        WorkspaceUpdateRequest request,
        OjSharpDbContext dbContext,
        WorkspaceProjectionService projectionService,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (questionIdText, update) in request.Questions)
        {
            if (!Guid.TryParse(questionIdText, out var questionId))
            {
                continue;
            }

            var questionExists = await dbContext.Questions.AnyAsync(
                question => question.Id == questionId && question.AssessmentId == assessmentId,
                cancellationToken);
            if (!questionExists)
            {
                return ApiResults.Error("QUESTION_NOT_FOUND", "Question was not found for this assessment.", StatusCodes.Status404NotFound);
            }

            var state = await dbContext.WorkspaceQuestionStates
                .FirstOrDefaultAsync(item => item.SessionId == sessionId && item.QuestionId == questionId, cancellationToken);
            if (state is null)
            {
                state = new WorkspaceQuestionState
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    QuestionId = questionId
                };
                dbContext.WorkspaceQuestionStates.Add(state);
            }

            state.SelectedLanguage = update.SelectedLanguage;
            state.ActiveFile = update.ActiveFile;
            state.FilesJson = JsonDocumentSerializer.Serialize(update.Files);
            state.LastSavedAt = now;
            state.Version = Math.Max(state.Version + 1, (update.Version ?? 0) + 1);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        var states = await dbContext.WorkspaceQuestionStates.Where(state => state.SessionId == sessionId).ToListAsync(cancellationToken);
        return ApiResults.Success(projectionService.ToWorkspace(sessionId, states));
    }

    private static async Task<(AssessmentSession? Session, IResult? Error)> ResolveActiveSessionAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (user, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Student, cancellationToken);
        if (error is not null)
        {
            return (null, error);
        }

        var session = await dbContext.AssessmentSessions.FirstOrDefaultAsync(
            item => item.AssessmentId == assessmentId
                    && item.UserId == user!.Id
                    && item.Status == SessionStatuses.Active
                    && item.ExpiresAt > DateTimeOffset.UtcNow,
            cancellationToken);
        return session is null
            ? (null, ApiResults.Error("ATTEMPT_NOT_FOUND", "Active assessment attempt was not found.", StatusCodes.Status404NotFound))
            : (session, null);
    }
}
