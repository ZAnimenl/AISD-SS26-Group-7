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

        var activeAssessments = await dbContext.Assessments
            .Where(assessment => assessment.Status == AssessmentStatuses.Active)
            .Include(assessment => assessment.Sessions.Where(session => session.UserId == user!.Id))
            .ToListAsync(cancellationToken);
        var sessions = await DateTimeOffsetOrdering.ToDescendingListAsync(
            dbContext.AssessmentSessions
                .Where(session => session.UserId == user!.Id)
                .Include(session => session.Assessment),
            dbContext,
            session => session.StartedAt,
            cancellationToken);
        var submissions = await dbContext.Submissions
            .Where(submission => submission.Session!.UserId == user!.Id)
            .ToListAsync(cancellationToken);

        var completedScores = submissions.Where(submission => submission.MaxScore > 0).ToList();
        return ApiResults.Success(new
        {
            summary = new
            {
                available_assessments = CountAvailableAssessments(activeAssessments, sessionClock, DateTimeOffset.UtcNow),
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

    internal static int CountAvailableAssessments(
        IEnumerable<Assessment> activeAssessments,
        SessionClock sessionClock,
        DateTimeOffset? now = null)
    {
        return activeAssessments.Count(assessment =>
            AssessmentPolicy.IsAssessmentAvailable(assessment, now)
            && assessment.Sessions.All(session =>
            {
                var status = sessionClock.GetEffectiveStatus(session);
                return status is not SessionStatuses.Active and not SessionStatuses.Expired;
            }));
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
                starts_at = assessment.StartsAt,
                assessment.Status,
                ai_enabled = assessment.AiEnabled,
                question_count = assessment.Questions.Count,
                questions = assessment.Questions
                    .OrderBy(question => question.SortOrder)
                    .Select(ToQuestionPreviewDto),
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
                var session = latestSubmission.Session!;
                var score = submissions.Sum(submission => submission.Score);
                var maxScore = submissions.Sum(submission => submission.MaxScore);
                var functionalScore = maxScore > 0
                    ? (int)Math.Round(score * 100.0 / maxScore, MidpointRounding.AwayFromZero)
                    : 0;
                var aiEnabled = session.Assessment!.AiEnabled;
                var aiUsageScore = session.AiUsageScore;

                return new StudentResultSummary(
                    SubmissionId: latestSubmission.Id,
                    AttemptId: latestSubmission.SessionId,
                    AssessmentId: session.AssessmentId,
                    AssessmentTitle: session.Assessment.Title,
                    EvaluationStatus: BuildResultStatus(submissions, score, maxScore),
                    Score: score,
                    MaxScore: maxScore,
                    FunctionalScore: functionalScore,
                    AiEnabled: aiEnabled,
                    AiUsageScore: aiUsageScore,
                    FinalScore: aiEnabled && aiUsageScore.HasValue
                        ? (int?)Math.Round((functionalScore + aiUsageScore.Value) / 2.0, MidpointRounding.AwayFromZero)
                        : null,
                    AiGradingStatus: session.AiGradingStatus,
                    ReflectionText: session.ReflectionText,
                    ReflectionSubmittedAt: session.ReflectionSubmittedAt,
                    QuestionCount: session.Assessment.Questions.Count,
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

        if (submissions.Any(submission => submission.EvaluationStatus == ExecutionStatuses.TimeLimitExceeded))
        {
            return ExecutionStatuses.TimeLimitExceeded;
        }

        return submissions.Any(submission => submission.EvaluationStatus == ExecutionStatuses.RuntimeError)
            ? ExecutionStatuses.RuntimeError
            : ExecutionStatuses.Failed;
    }

    private static object ToQuestionPreviewDto(Question question)
    {
        return new
        {
            question_id = question.Id,
            question.Title,
            task_type = question.TaskType,
            difficulty = question.Difficulty,
            verification_mode = question.VerificationMode,
            language_constraints = JsonDocumentSerializer.Deserialize(question.LanguageConstraintsJson, Array.Empty<string>())
        };
    }

    internal sealed record StudentResultSummary(
        Guid SubmissionId,
        Guid AttemptId,
        Guid AssessmentId,
        string AssessmentTitle,
        string EvaluationStatus,
        int Score,
        int MaxScore,
        int FunctionalScore,
        bool AiEnabled,
        int? AiUsageScore,
        int? FinalScore,
        string AiGradingStatus,
        string ReflectionText,
        DateTimeOffset? ReflectionSubmittedAt,
        int QuestionCount,
        DateTimeOffset SubmittedAt);
}
