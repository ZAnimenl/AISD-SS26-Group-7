using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api;

public static class StudentEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var group = api.MapGroup("/student");

        group.MapGet("/dashboard", DashboardAsync);
        group.MapGet("/assessments", AssessmentsAsync);
        group.MapGet("/results", ResultsAsync);
    }

    private static async Task<IResult> DashboardAsync(
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

        var activeAssessments = await dbContext.Assessments.CountAsync(assessment => assessment.Status == AssessmentStatuses.Active, cancellationToken);
        var sessions = await dbContext.AssessmentSessions
            .Where(session => session.UserId == user!.Id)
            .Include(session => session.Assessment)
            .OrderByDescending(session => session.StartedAt)
            .ToListAsync(cancellationToken);
        var submissions = await dbContext.Submissions
            .Where(submission => submission.Session!.UserId == user!.Id)
            .ToListAsync(cancellationToken);

        var completedScores = submissions.Where(submission => submission.MaxScore > 0).ToList();
        return ApiResults.Success(new
        {
            summary = new
            {
                available_assessments = activeAssessments,
                in_progress_sessions = sessions.Count(session => sessionClock.GetEffectiveStatus(session) == SessionStatuses.Active),
                completed_assessments = sessions.Count(session => session.Status == SessionStatuses.Submitted),
                average_score = completedScores.Count == 0 ? 0 : completedScores.Average(submission => submission.Score)
            },
            recent_activity = sessions.Take(5).Select(session => new
            {
                session_id = session.Id,
                assessment_id = session.AssessmentId,
                assessment_title = session.Assessment?.Title,
                session_status = sessionClock.GetEffectiveStatus(session),
                session.StartedAt,
                session.ExpiresAt
            })
        });
    }

    private static async Task<IResult> AssessmentsAsync(
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

        var assessments = await dbContext.Assessments
            .Where(assessment => assessment.Status == AssessmentStatuses.Active)
            .Include(assessment => assessment.Questions)
            .Include(assessment => assessment.Sessions.Where(session => session.UserId == user!.Id))
            .OrderBy(assessment => assessment.Title)
            .ToListAsync(cancellationToken);

        return ApiResults.Success(assessments.Select(assessment =>
        {
            var session = assessment.Sessions.OrderByDescending(item => item.StartedAt).FirstOrDefault();
            return new
            {
                assessment_id = assessment.Id,
                assessment.Title,
                assessment.Description,
                duration_minutes = assessment.DurationMinutes,
                assessment.Status,
                question_count = assessment.Questions.Count,
                session_id = session?.Id,
                session_status = session is null ? SessionStatuses.NotStarted : sessionClock.GetEffectiveStatus(session)
            };
        }));
    }

    private static async Task<IResult> ResultsAsync(
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

        var results = await dbContext.Submissions
            .Where(submission => submission.Session!.UserId == user!.Id)
            .Include(submission => submission.Session)
            .ThenInclude(session => session!.Assessment)
            .ThenInclude(assessment => assessment!.Questions)
            .ToListAsync(cancellationToken);

        return ApiResults.Success(BuildResultSummaries(results));
    }

    internal static IReadOnlyList<StudentResultSummary> BuildResultSummaries(IEnumerable<Submission> results)
    {
        return results
            .GroupBy(submission => submission.SessionId)
            .Select(group =>
            {
                var submissions = group.ToList();
                var latestSubmission = submissions.OrderByDescending(submission => submission.SubmittedAt).First();
                var score = submissions.Sum(submission => submission.Score);
                var maxScore = submissions.Sum(submission => submission.MaxScore);

                return new StudentResultSummary(
                    SubmissionId: latestSubmission.Id,
                    SessionId: latestSubmission.SessionId,
                    AssessmentId: latestSubmission.Session!.AssessmentId,
                    AssessmentTitle: latestSubmission.Session.Assessment!.Title,
                    EvaluationStatus: BuildResultStatus(submissions, score, maxScore),
                    Score: score,
                    MaxScore: maxScore,
                    QuestionCount: latestSubmission.Session.Assessment.Questions.Count,
                    SubmittedAt: submissions.Max(submission => submission.SubmittedAt));
            })
            .OrderByDescending(result => result.SubmittedAt)
            .ToList();
    }

    private static string BuildResultStatus(IReadOnlyCollection<Submission> submissions, int score, int maxScore)
    {
        if (maxScore > 0 && score == maxScore)
        {
            return ExecutionStatuses.Passed;
        }

        return submissions.Any(submission => submission.EvaluationStatus == ExecutionStatuses.RuntimeError)
            ? ExecutionStatuses.RuntimeError
            : ExecutionStatuses.Failed;
    }

    internal sealed record StudentResultSummary(
        Guid SubmissionId,
        Guid SessionId,
        Guid AssessmentId,
        string AssessmentTitle,
        string EvaluationStatus,
        int Score,
        int MaxScore,
        int QuestionCount,
        DateTimeOffset SubmittedAt);
}
