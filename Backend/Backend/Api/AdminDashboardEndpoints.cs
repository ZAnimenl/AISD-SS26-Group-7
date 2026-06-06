using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api;

public static class AdminDashboardEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/admin/dashboard", DashboardAsync);
    }

    private static async Task<IResult> DashboardAsync(
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

        var submissions = await dbContext.Submissions.ToListAsync(cancellationToken);
        var usesSqlite = DatabaseProviders.IsSqliteProviderName(dbContext.Database.ProviderName);
        var recentAssessments = usesSqlite
            ? (await dbContext.Assessments
                .Select(assessment => new
                {
                    assessment_id = assessment.Id,
                    assessment.Title,
                    assessment.Status,
                    created_at = assessment.CreatedAt
                })
                .ToListAsync(cancellationToken))
                .OrderByDescending(assessment => assessment.created_at)
                .Take(5)
                .ToList()
            : await dbContext.Assessments
                .OrderByDescending(assessment => assessment.CreatedAt)
                .Take(5)
                .Select(assessment => new
                {
                    assessment_id = assessment.Id,
                    assessment.Title,
                    assessment.Status,
                    created_at = assessment.CreatedAt
                })
                .ToListAsync(cancellationToken);
        var recentSubmissions = usesSqlite
            ? (await dbContext.Submissions
                .Select(submission => new
                {
                    submission_id = submission.Id,
                    student_name = submission.Session!.User!.FullName,
                    assessment_title = submission.Session.Assessment!.Title,
                    submission.Score,
                    max_score = submission.MaxScore,
                    submitted_at = submission.SubmittedAt
                })
                .ToListAsync(cancellationToken))
                .OrderByDescending(submission => submission.submitted_at)
                .Take(5)
                .ToList()
            : await dbContext.Submissions
                .OrderByDescending(submission => submission.SubmittedAt)
                .Take(5)
                .Select(submission => new
                {
                    submission_id = submission.Id,
                    student_name = submission.Session!.User!.FullName,
                    assessment_title = submission.Session.Assessment!.Title,
                    submission.Score,
                    max_score = submission.MaxScore,
                    submitted_at = submission.SubmittedAt
                })
                .ToListAsync(cancellationToken);

        return ApiResults.Success(new
        {
            summary = new
            {
                total_assessments = await dbContext.Assessments.CountAsync(cancellationToken),
                active_assessments = await dbContext.Assessments.CountAsync(assessment => assessment.Status == AssessmentStatuses.Active, cancellationToken),
                total_students = await dbContext.Users.CountAsync(user => user.Role == UserRoles.Student, cancellationToken),
                total_submissions = submissions.Count,
                average_score = submissions.Count == 0 ? 0 : submissions.Average(submission => submission.Score),
                ai_interactions = await dbContext.AiInteractions.CountAsync(cancellationToken)
            },
            recent_assessments = recentAssessments,
            recent_submissions = recentSubmissions
        });
    }
}
