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
        group.MapGet("/results/{assessmentId:guid}", ResultDetailAsync);
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
                in_progress_attempts = sessions.Count(session => sessionClock.GetEffectiveStatus(session) == SessionStatuses.Active),
                completed_assessments = sessions.Count(session => session.Status == SessionStatuses.Submitted),
                average_score = completedScores.Count == 0 ? 0 : completedScores.Average(submission => submission.Score)
            },
            recent_activity = sessions.Take(5).Select(session => new
            {
                attempt_id = session.Id,
                assessment_id = session.AssessmentId,
                assessment_title = session.Assessment?.Title,
                attempt_status = sessionClock.GetEffectiveStatus(session),
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
                ai_enabled = assessment.AiEnabled,
                question_count = assessment.Questions.Count,
                attempt_id = session?.Id,
                attempt_status = session is null ? SessionStatuses.NotStarted : sessionClock.GetEffectiveStatus(session)
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

        return ApiResults.Success(BuildResultSummaries(results.Where(submission => submission.Session!.Assessment!.ReportsReleased)));
    }

    private static async Task<IResult> ResultDetailAsync(
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

        var assessment = await dbContext.Assessments.FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        if (!assessment.ReportsReleased)
        {
            return ApiResults.Error("REPORT_NOT_RELEASED", "Final reports have not been released for this assessment.", StatusCodes.Status403Forbidden);
        }

        var session = await dbContext.AssessmentSessions
            .Include(item => item.Assessment)
            .ThenInclude(item => item!.Questions)
            .Include(item => item.Submissions)
            .FirstOrDefaultAsync(item => item.AssessmentId == assessmentId && item.UserId == user!.Id, cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("ATTEMPT_NOT_FOUND", "Assessment attempt was not found.", StatusCodes.Status404NotFound);
        }

        var submissions = session.Submissions.OrderByDescending(submission => submission.SubmittedAt).ToList();
        if (submissions.Count == 0)
        {
            return ApiResults.Error("NOT_FOUND", "No released submission result was found.", StatusCodes.Status404NotFound);
        }

        var score = submissions.Sum(submission => submission.Score);
        var maxScore = submissions.Sum(submission => submission.MaxScore);
        return ApiResults.Success(new
        {
            assessment_id = assessmentId,
            assessment_title = session.Assessment!.Title,
            attempt_id = session.Id,
            evaluation_status = BuildResultStatus(submissions, score, maxScore),
            score,
            max_score = maxScore,
            process_score = ToProcessScoreDto(session),
            short_explanation = session.ProcessScoreExplanationJson is null
                ? "Released result is based on stored code correctness and available process signals."
                : null,
            submissions = submissions.Select(submission => new
            {
                submission_id = submission.Id,
                question_id = submission.QuestionId,
                evaluation_status = submission.EvaluationStatus,
                submission.Score,
                max_score = submission.MaxScore,
                submitted_at = submission.SubmittedAt
            })
        });
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
                    AttemptId: latestSubmission.SessionId,
                    AssessmentId: latestSubmission.Session!.AssessmentId,
                    AssessmentTitle: latestSubmission.Session.Assessment!.Title,
                    EvaluationStatus: BuildResultStatus(submissions, score, maxScore),
                    Score: score,
                    MaxScore: maxScore,
                    ProcessAwareScore: latestSubmission.Session.ProcessAwareScore,
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
        Guid AttemptId,
        Guid AssessmentId,
        string AssessmentTitle,
        string EvaluationStatus,
        int Score,
        int MaxScore,
        int? ProcessAwareScore,
        int QuestionCount,
        DateTimeOffset SubmittedAt);

    private static object ToProcessScoreDto(AssessmentSession session)
    {
        return new
        {
            final_score = session.ProcessAwareScore,
            code_correctness = session.CodeCorrectnessScore,
            ai_usage_quality = session.AiUsageQualityScore,
            reflection_understanding = session.ReflectionUnderstandingScore,
            critical_ai_judgment = session.CriticalAiJudgmentScore,
            explanations = session.ProcessScoreExplanationJson is null
                ? null
                : JsonDocumentSerializer.Deserialize(session.ProcessScoreExplanationJson, new Dictionary<string, object>())
        };
    }
}
