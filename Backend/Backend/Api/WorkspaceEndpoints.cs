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
            requireOpenAssessment: false,
            cancellationToken: cancellationToken);
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
            requireOpenAssessment: true,
            cancellationToken: cancellationToken);
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

            var question = await dbContext.Questions.FirstOrDefaultAsync(
                question => question.Id == questionId && question.AssessmentId == assessmentId,
                cancellationToken);
            if (question is null)
            {
                return ApiResults.Error("QUESTION_NOT_FOUND", "Question was not found for this assessment.", StatusCodes.Status404NotFound);
            }

            var selectedLanguage = AssessmentPolicy.NormalizeLanguage(update.SelectedLanguage);
            var normalizedFiles = update.Files.ToDictionary(
                entry => entry.Key,
                entry => new WorkspaceFileRequest(
                    AssessmentPolicy.NormalizeLanguage(entry.Value.Language),
                    entry.Value.Content));
            if (AssessmentPolicy.TryFindUnsupportedWorkspaceLanguage(
                    question,
                    selectedLanguage,
                    normalizedFiles.Values.Select(file => file.Language),
                    out var unsupportedLanguage))
            {
                return ApiResults.Error(
                    "LANGUAGE_NOT_ALLOWED",
                    $"Language '{unsupportedLanguage}' is not allowed for this task.",
                    StatusCodes.Status400BadRequest);
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

            state.SelectedLanguage = selectedLanguage;
            state.ActiveFile = update.ActiveFile;
            state.FilesJson = JsonDocumentSerializer.Serialize(normalizedFiles);
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
        bool requireOpenAssessment,
        CancellationToken cancellationToken)
    {
        var (user, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Student, cancellationToken);
        if (error is not null)
        {
            return (null, error);
        }

        var session = await SessionQueries.FirstUnexpiredAsync(
            dbContext.AssessmentSessions
                .Include(item => item.Assessment)
                .Where(item => item.AssessmentId == assessmentId
                               && item.UserId == user!.Id
                               && item.Status == SessionStatuses.Active),
            dbContext,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (session is null)
        {
            return (null, ApiResults.Error("ATTEMPT_NOT_FOUND", "Active assessment attempt was not found.", StatusCodes.Status404NotFound));
        }

        if (requireOpenAssessment && !AssessmentPolicy.IsAssessmentActive(session.Assessment))
        {
            return (null, ApiResults.Error("ASSESSMENT_CLOSED", "This assessment is not accepting workspace changes.", StatusCodes.Status409Conflict));
        }

        return (session, null);
    }
}
