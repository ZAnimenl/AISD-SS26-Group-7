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
        var session = await dbContext.AssessmentSessions
            .Include(item => item.WorkspaceStates)
            .FirstOrDefaultAsync(
                item => item.AssessmentId == assessmentId
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

        foreach (var question in assessment.Questions.Where(question => !existingQuestionIds.Contains(question.Id)))
        {
            var starterCode = JsonDocumentSerializer.Deserialize(question.StarterCodeJson, new Dictionary<string, string>());
            var language = starterCode.ContainsKey("python") ? "python" : starterCode.Keys.FirstOrDefault() ?? "python";
            var activeFile = GetActiveFile(language);
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

    private static string GetActiveFile(string language)
    {
        return language switch
        {
            "javascript" => "main.js",
            "typescript" => "main.ts",
            _ => "main.py"
        };
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
