using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api;

public static class ReportEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/admin/reports", ListAsync);
        api.MapGet("/reports/aggregate/{assessmentId:guid}", AggregateAsync);
        api.MapGet("/admin/reports/{assessmentId:guid}/students/{studentId:guid}", StudentDetailAsync);
    }

    internal sealed record AiUsageSummary(
        int TotalInteractions,
        int TotalInputTokens,
        int TotalOutputTokens,
        int TotalTokens,
        int AverageTokensPerInteraction,
        string TokenEfficiencyIndicator,
        IReadOnlyList<string> MainSemanticTags,
        IReadOnlyList<AiTaskTokenTotal> PerTaskTokenTotals);

    internal sealed record AiTaskTokenTotal(
        Guid QuestionId,
        string TaskTitle,
        string TaskType,
        int InteractionCount,
        int TotalInputTokens,
        int TotalOutputTokens,
        int TotalTokens);

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var assessments = await DateTimeOffsetOrdering.ToDescendingListAsync(
            dbContext.Assessments.Include(assessment => assessment.Sessions),
            dbContext,
            assessment => assessment.CreatedAt,
            cancellationToken);
        var assessmentIds = assessments.Select(assessment => assessment.Id).ToArray();
        var submissionSummaries = await dbContext.Submissions
            .Where(submission => assessmentIds.Contains(submission.Session!.AssessmentId))
            .Select(submission => new
            {
                AssessmentId = submission.Session!.AssessmentId,
                submission.SessionId,
                submission.Score,
                submission.MaxScore
            })
            .ToListAsync(cancellationToken);
        var aiSummaries = await dbContext.AiInteractions
            .Where(interaction => assessmentIds.Contains(interaction.AssessmentId))
            .Select(interaction => new
            {
                interaction.AssessmentId,
                interaction.TotalTokens
            })
            .ToListAsync(cancellationToken);

        var reports = assessments.Select(assessment =>
        {
            var assessmentSubmissions = submissionSummaries
                .Where(submission => submission.AssessmentId == assessment.Id)
                .ToList();
            var scores = assessmentSubmissions
                .GroupBy(submission => submission.SessionId)
                .Select(group => new
                {
                    Score = group.Sum(submission => submission.Score),
                    MaxScore = group.Sum(submission => submission.MaxScore)
                })
                .Where(summary => summary.MaxScore > 0)
                .Select(summary => summary.Score * 100.0 / summary.MaxScore)
                .ToList();
            var assessmentAi = aiSummaries
                .Where(interaction => interaction.AssessmentId == assessment.Id)
                .ToList();

            return new
            {
                assessment_id = assessment.Id,
                assessment_title = assessment.Title,
                assessment.Status,
                average_score = scores.Count == 0 ? 0 : scores.Average(),
                participant_count = assessment.Sessions.Count,
                completion_count = assessment.Sessions.Count(session => session.Status == SessionStatuses.Submitted),
                ai_interactions = assessmentAi.Count,
                total_ai_tokens = assessmentAi.Sum(interaction => interaction.TotalTokens)
            };
        }).ToList();

        return ApiResults.Success(reports);
    }

    private static async Task<IResult> AggregateAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var assessment = await dbContext.Assessments
            .Include(item => item.Sessions)
            .ThenInclude(session => session.User)
            .Include(item => item.Questions)
            .FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        var submissions = await dbContext.Submissions
            .Where(submission => submission.Session!.AssessmentId == assessmentId)
            .Include(submission => submission.Session)
            .ThenInclude(session => session!.User)
            .ToListAsync(cancellationToken);
        var bySession = submissions.GroupBy(submission => submission.SessionId)
            .ToDictionary(group => group.Key, group =>
            {
                var sessionSubmissions = group.ToList();
                var score = sessionSubmissions.Sum(submission => submission.Score);
                var maxScore = sessionSubmissions.Sum(submission => submission.MaxScore);
                return new
                {
                    Score = score,
                    MaxScore = maxScore,
                    Status = BuildSubmissionStatus(sessionSubmissions, score, maxScore),
                    SubmittedAt = sessionSubmissions.Max(submission => submission.SubmittedAt)
                };
            });
        var interactions = await dbContext.AiInteractions
            .Where(interaction => interaction.AssessmentId == assessmentId)
            .ToListAsync(cancellationToken);
        var interactionsBySession = interactions
            .GroupBy(interaction => interaction.SessionId)
            .ToDictionary(group => group.Key, group => group.AsEnumerable());
        var questionLookup = assessment.Questions.ToDictionary(question => question.Id);

        var students = new List<object>();
        foreach (var session in assessment.Sessions.OrderBy(item => item.User!.FullName))
        {
            bySession.TryGetValue(session.Id, out var summary);
            interactionsBySession.TryGetValue(session.Id, out var sessionInteractions);
            students.Add(new
            {
                user_id = session.UserId,
                student_name = session.User!.FullName,
                student_email = session.User.Email,
                attempt_status = session.Status,
                submission_status = summary?.Status ?? "not_submitted",
                score = summary?.Score ?? 0,
                max_score = summary?.MaxScore ?? 0,
                submitted_at = summary?.SubmittedAt,
                ai_usage_summary = BuildAiUsageSummary(
                    sessionInteractions ?? Array.Empty<AiInteraction>(),
                    questionLookup,
                    summary?.Score ?? 0,
                    summary?.MaxScore ?? 0)
            });
        }

        var scores = bySession.Values.Select(summary => summary.MaxScore == 0 ? 0 : summary.Score * 100.0 / summary.MaxScore).ToList();
        var totalScore = bySession.Values.Sum(summary => summary.Score);
        var totalMaxScore = bySession.Values.Sum(summary => summary.MaxScore);
        return ApiResults.Success(new
        {
            assessment_id = assessment.Id,
            assessment_title = assessment.Title,
            average_score = scores.Count == 0 ? 0 : scores.Average(),
            completion_count = assessment.Sessions.Count(session => session.Status == SessionStatuses.Submitted),
            participant_count = assessment.Sessions.Count,
            ai_interactions = interactions.Count,
            ai_usage_summary = BuildAiUsageSummary(interactions, questionLookup, totalScore, totalMaxScore),
            score_distribution = BuildScoreDistribution(scores),
            students
        });
    }

    private static async Task<IResult> StudentDetailAsync(
        Guid assessmentId,
        Guid studentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var session = await dbContext.AssessmentSessions
            .Include(item => item.User)
            .Include(item => item.Assessment)
            .ThenInclude(assessment => assessment!.Questions)
            .FirstOrDefaultAsync(item => item.AssessmentId == assessmentId && item.UserId == studentId, cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("ATTEMPT_NOT_FOUND", "Assessment attempt was not found.", StatusCodes.Status404NotFound);
        }

        var submissions = await DateTimeOffsetOrdering.ToDescendingListAsync(
            dbContext.Submissions
                .Where(submission => submission.SessionId == session.Id)
                .Select(submission => new
                {
                    submission_id = submission.Id,
                    question_id = submission.QuestionId,
                    evaluation_status = submission.EvaluationStatus,
                    submission.Score,
                    max_score = submission.MaxScore,
                    submitted_at = submission.SubmittedAt
                }),
            dbContext,
            submission => submission.submitted_at,
            cancellationToken);

        var aiInteractions = await DateTimeOffsetOrdering.ToAscendingListAsync(
            dbContext.AiInteractions.Where(interaction => interaction.SessionId == session.Id),
            dbContext,
            interaction => interaction.CreatedAt,
            cancellationToken);
        var interactions = aiInteractions
            .Select(interaction => new
            {
                interaction_id = interaction.Id,
                question_id = interaction.QuestionId,
                interaction_type = interaction.InteractionType,
                interaction.Message,
                selected_language = interaction.SelectedLanguage,
                semantic_tags = JsonDocumentSerializer.Deserialize(interaction.SemanticTagsJson, Array.Empty<string>()),
                token_usage = new
                {
                    input_tokens = interaction.InputTokens,
                    output_tokens = interaction.OutputTokens,
                    total_tokens = interaction.TotalTokens
                },
                created_at = interaction.CreatedAt
            })
            .ToList();
        var score = submissions.Sum(submission => submission.Score);
        var maxScore = submissions.Sum(submission => submission.max_score);
        var questionLookup = session.Assessment!.Questions.ToDictionary(question => question.Id);

        return ApiResults.Success(new
        {
            assessment_id = assessmentId,
            assessment_title = session.Assessment!.Title,
            student = AuthEndpoints.ToUserDto(session.User!),
            attempt_id = session.Id,
            attempt_status = session.Status,
            submissions,
            ai_interactions = interactions,
            ai_usage_summary = BuildAiUsageSummary(aiInteractions, questionLookup, score, maxScore)
        });
    }

    internal static AiUsageSummary BuildAiUsageSummary(
        IEnumerable<AiInteraction> interactions,
        IReadOnlyDictionary<Guid, Question> questions,
        int score,
        int maxScore)
    {
        var interactionList = interactions.ToList();
        var totalInputTokens = interactionList.Sum(interaction => interaction.InputTokens);
        var totalOutputTokens = interactionList.Sum(interaction => interaction.OutputTokens);
        var totalTokens = interactionList.Sum(interaction => interaction.TotalTokens);
        var averageTokensPerInteraction = interactionList.Count == 0 ? 0 : totalTokens / interactionList.Count;
        var tags = interactionList
            .SelectMany(interaction => JsonDocumentSerializer.Deserialize(interaction.SemanticTagsJson, Array.Empty<string>()))
            .GroupBy(tag => tag)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Take(5)
            .Select(group => group.Key)
            .ToArray();
        var perTaskTokenTotals = interactionList
            .GroupBy(interaction => interaction.QuestionId)
            .Select(group =>
            {
                questions.TryGetValue(group.Key, out var question);
                return new AiTaskTokenTotal(
                    group.Key,
                    question?.Title ?? "Unknown task",
                    question?.TaskType ?? string.Empty,
                    group.Count(),
                    group.Sum(interaction => interaction.InputTokens),
                    group.Sum(interaction => interaction.OutputTokens),
                    group.Sum(interaction => interaction.TotalTokens));
            })
            .OrderBy(task => questions.TryGetValue(task.QuestionId, out var question) ? question.SortOrder : int.MaxValue)
            .ThenBy(task => task.TaskTitle)
            .ToArray();

        return new AiUsageSummary(
            interactionList.Count,
            totalInputTokens,
            totalOutputTokens,
            totalTokens,
            averageTokensPerInteraction,
            BuildTokenEfficiencyIndicator(score, maxScore, totalTokens, interactionList.Count),
            tags,
            perTaskTokenTotals);
    }

    internal static string BuildTokenEfficiencyIndicator(
        int score,
        int maxScore,
        int totalTokens,
        int totalInteractions)
    {
        if (totalInteractions == 0 || totalTokens == 0)
        {
            return "no_ai_usage";
        }

        var scorePercent = maxScore <= 0 ? 0 : score * 100.0 / maxScore;
        if (scorePercent >= 80 && totalTokens <= 2500)
        {
            return "strategic";
        }

        if (scorePercent >= 80)
        {
            return "token_heavy_success";
        }

        if (scorePercent < 60 && totalTokens >= 2500)
        {
            return "inefficient";
        }

        return "needs_review";
    }

    private static string BuildSubmissionStatus(IReadOnlyCollection<Submission> submissions, int score, int maxScore)
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

    private static object[] BuildScoreDistribution(IEnumerable<double> scores)
    {
        var values = scores.ToList();
        return
        [
            new { range = "0-20", count = values.Count(score => score is >= 0 and <= 20) },
            new { range = "21-40", count = values.Count(score => score is > 20 and <= 40) },
            new { range = "41-60", count = values.Count(score => score is > 40 and <= 60) },
            new { range = "61-80", count = values.Count(score => score is > 60 and <= 80) },
            new { range = "81-100", count = values.Count(score => score is > 80 and <= 100) }
        ];
    }
}
