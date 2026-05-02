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
        api.MapGet("/sessions/{sessionId:guid}/workspace", GetAsync);
        api.MapPut("/sessions/{sessionId:guid}/workspace", SaveAsync);
    }

    private static async Task<IResult> GetAsync(
        Guid sessionId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        WorkspaceProjectionService projectionService,
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

        var states = await dbContext.WorkspaceQuestionStates
            .Where(state => state.SessionId == sessionId)
            .OrderBy(state => state.QuestionId)
            .ToListAsync(cancellationToken);
        return ApiResults.Success(projectionService.ToWorkspace(sessionId, states));
    }

    private static async Task<IResult> SaveAsync(
        Guid sessionId,
        WorkspaceUpdateRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SessionClock sessionClock,
        WorkspaceProjectionService projectionService,
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

        if (sessionClock.IsClosed(session))
        {
            return ApiResults.Error("SESSION_EXPIRED", "The assessment session has expired.", StatusCodes.Status409Conflict);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var (questionIdText, update) in request.Questions)
        {
            if (!Guid.TryParse(questionIdText, out var questionId))
            {
                continue;
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
}
