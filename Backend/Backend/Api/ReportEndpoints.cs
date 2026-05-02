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

        var reports = await dbContext.Assessments
            .OrderByDescending(assessment => assessment.CreatedAt)
            .Select(assessment => new
            {
                assessment_id = assessment.Id,
                assessment_title = assessment.Title,
                assessment.Status,
                participant_count = assessment.Sessions.Count,
                completion_count = assessment.Sessions.Count(session => session.Status == SessionStatuses.Submitted)
            })
            .ToListAsync(cancellationToken);

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
            .ToDictionary(group => group.Key, group => new
            {
                Score = group.Sum(submission => submission.Score),
                MaxScore = group.Sum(submission => submission.MaxScore),
                Status = group.All(submission => submission.EvaluationStatus == ExecutionStatuses.Passed)
                    ? ExecutionStatuses.Passed
                    : ExecutionStatuses.Failed,
                SubmittedAt = group.Max(submission => submission.SubmittedAt)
            });

        var students = new List<object>();
        foreach (var session in assessment.Sessions.OrderBy(item => item.User!.FullName))
        {
            bySession.TryGetValue(session.Id, out var summary);
            var tags = await dbContext.AiInteractions
                .Where(interaction => interaction.SessionId == session.Id)
                .Select(interaction => interaction.SemanticTagsJson)
                .ToListAsync(cancellationToken);
            students.Add(new
            {
                user_id = session.UserId,
                student_name = session.User!.FullName,
                student_email = session.User.Email,
                session_status = session.Status,
                submission_status = summary?.Status,
                score = summary?.Score ?? 0,
                max_score = summary?.MaxScore ?? 0,
                submitted_at = summary?.SubmittedAt,
                ai_usage_summary = new
                {
                    total_interactions = tags.Count,
                    main_semantic_tags = tags.SelectMany(tag => JsonDocumentSerializer.Deserialize(tag, Array.Empty<string>())).Distinct().ToArray()
                }
            });
        }

        var scores = bySession.Values.Select(summary => summary.MaxScore == 0 ? 0 : summary.Score * 100.0 / summary.MaxScore).ToList();
        return ApiResults.Success(new
        {
            assessment_id = assessment.Id,
            assessment_title = assessment.Title,
            average_score = scores.Count == 0 ? 0 : scores.Average(),
            completion_count = assessment.Sessions.Count(session => session.Status == SessionStatuses.Submitted),
            participant_count = assessment.Sessions.Count,
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
            .FirstOrDefaultAsync(item => item.AssessmentId == assessmentId && item.UserId == studentId, cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("SESSION_NOT_FOUND", "Session was not found.", StatusCodes.Status404NotFound);
        }

        var submissions = await dbContext.Submissions
            .Where(submission => submission.SessionId == session.Id)
            .OrderByDescending(submission => submission.SubmittedAt)
            .Select(submission => new
            {
                submission_id = submission.Id,
                question_id = submission.QuestionId,
                evaluation_status = submission.EvaluationStatus,
                submission.Score,
                max_score = submission.MaxScore,
                submitted_at = submission.SubmittedAt
            })
            .ToListAsync(cancellationToken);

        var interactions = await dbContext.AiInteractions
            .Where(interaction => interaction.SessionId == session.Id)
            .OrderBy(interaction => interaction.CreatedAt)
            .Select(interaction => new
            {
                interaction_id = interaction.Id,
                interaction_type = interaction.InteractionType,
                interaction.Message,
                semantic_tags = JsonDocumentSerializer.Deserialize(interaction.SemanticTagsJson, Array.Empty<string>()),
                created_at = interaction.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return ApiResults.Success(new
        {
            assessment_id = assessmentId,
            assessment_title = session.Assessment!.Title,
            student = AuthEndpoints.ToUserDto(session.User!),
            session_id = session.Id,
            session_status = session.Status,
            submissions,
            ai_interactions = interactions
        });
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
