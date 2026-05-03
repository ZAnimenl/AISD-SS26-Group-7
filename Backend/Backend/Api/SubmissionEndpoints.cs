using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api;

public static class SubmissionEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/submissions/finalize", FinalizeAsync);
        api.MapGet("/sessions/{sessionId:guid}/submissions", HistoryAsync);
        api.MapGet("/admin/submissions/{submissionId:guid}", AdminDetailAsync);
    }

    private static async Task<IResult> FinalizeAsync(
        FinalizeSubmissionRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SessionClock sessionClock,
        CodeEvaluationService evaluationService,
        CancellationToken cancellationToken)
    {
        var (user, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Student, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var session = await dbContext.AssessmentSessions
            .Include(item => item.Assessment)
            .ThenInclude(assessment => assessment!.Questions)
            .Include(item => item.WorkspaceStates)
            .FirstOrDefaultAsync(item => item.Id == request.SessionId && item.UserId == user!.Id, cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("SESSION_NOT_FOUND", "Session was not found.", StatusCodes.Status404NotFound);
        }

        if (sessionClock.IsClosed(session))
        {
            return ApiResults.Error("SESSION_EXPIRED", "The assessment session has expired.", StatusCodes.Status409Conflict);
        }

        var allSubmissions = new List<Submission>();
        foreach (var state in session.WorkspaceStates)
        {
            var question = session.Assessment!.Questions.First(item => item.Id == state.QuestionId);
            var tests = await dbContext.TestCases.Where(testCase => testCase.QuestionId == state.QuestionId).ToListAsync(cancellationToken);
            var files = JsonDocumentSerializer.Deserialize(state.FilesJson, new Dictionary<string, WorkspaceFileDto>());
            var content = files.TryGetValue(state.ActiveFile, out var activeFile)
                ? activeFile.Content
                : files.Values.FirstOrDefault()?.Content ?? string.Empty;
            var result = await evaluationService.EvaluateAsync(
                Guid.NewGuid(),
                tests,
                content,
                state.SelectedLanguage,
                cancellationToken);
            var score = evaluationService.CalculateScore(question.MaxScore, result.TestResults);
            var visibleTotal = tests.Count(testCase => testCase.Visibility == TestCaseVisibilities.Public);
            var hiddenTotal = tests.Count(testCase => testCase.Visibility == TestCaseVisibilities.Hidden);
            var submission = new Submission
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                QuestionId = state.QuestionId,
                EvaluationStatus = result.Status,
                Score = score,
                MaxScore = question.MaxScore,
                Stdout = result.Stdout,
                Stderr = result.Stderr,
                FilesJson = state.FilesJson,
                VisiblePassed = result.TestResults.Count(testResult => testResult.Visibility == TestCaseVisibilities.Public && testResult.Passed),
                VisibleFailed = result.TestResults.Count(testResult => testResult.Visibility == TestCaseVisibilities.Public && !testResult.Passed),
                VisibleTotal = visibleTotal,
                HiddenPassed = result.TestResults.Count(testResult => testResult.Visibility == TestCaseVisibilities.Hidden && testResult.Passed),
                HiddenFailed = result.TestResults.Count(testResult => testResult.Visibility == TestCaseVisibilities.Hidden && !testResult.Passed),
                HiddenTotal = hiddenTotal,
                SubmittedAt = DateTimeOffset.UtcNow
            };
            allSubmissions.Add(submission);
            dbContext.Submissions.Add(submission);
        }

        session.Status = SessionStatuses.Submitted;
        session.CompletedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var totalScore = allSubmissions.Sum(submission => submission.Score);
        var maxScore = allSubmissions.Sum(submission => submission.MaxScore);
        var status = BuildFinalStatus(allSubmissions, totalScore, maxScore);
        return ApiResults.Success(new
        {
            submission_id = allSubmissions.FirstOrDefault()?.Id ?? Guid.Empty,
            evaluation_status = status,
            score = totalScore,
            max_score = maxScore,
            stdout = status == ExecutionStatuses.Passed ? "All tests passed." : null,
            stderr = status == ExecutionStatuses.Passed ? null : "One or more questions failed.",
            submitted_at = session.CompletedAt,
            visible_test_summary = new
            {
                passed = allSubmissions.Sum(submission => submission.VisiblePassed),
                failed = allSubmissions.Sum(submission => submission.VisibleFailed),
                total = allSubmissions.Sum(submission => submission.VisibleTotal)
            },
            hidden_test_summary = new
            {
                passed = allSubmissions.Sum(submission => submission.HiddenPassed),
                failed = allSubmissions.Sum(submission => submission.HiddenFailed),
                total = allSubmissions.Sum(submission => submission.HiddenTotal)
            }
        });
    }

    private static string BuildFinalStatus(IReadOnlyCollection<Submission> submissions, int totalScore, int maxScore)
    {
        if (maxScore > 0 && totalScore == maxScore)
        {
            return ExecutionStatuses.Passed;
        }

        return submissions.Any(submission => submission.EvaluationStatus == ExecutionStatuses.RuntimeError)
            ? ExecutionStatuses.RuntimeError
            : ExecutionStatuses.Failed;
    }

    private static async Task<IResult> HistoryAsync(
        Guid sessionId,
        Guid? questionId,
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

        var session = await dbContext.AssessmentSessions.FirstOrDefaultAsync(item => item.Id == sessionId && item.UserId == user!.Id, cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("SESSION_NOT_FOUND", "Session was not found.", StatusCodes.Status404NotFound);
        }

        var query = dbContext.Submissions.Where(submission => submission.SessionId == sessionId);
        if (questionId.HasValue)
        {
            query = query.Where(submission => submission.QuestionId == questionId.Value);
        }

        var submissions = await query
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
        return ApiResults.Success(submissions);
    }

    private static async Task<IResult> AdminDetailAsync(
        Guid submissionId,
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

        var submission = await dbContext.Submissions
            .Include(item => item.Session)
            .ThenInclude(session => session!.User)
            .Include(item => item.Session)
            .ThenInclude(session => session!.Assessment)
            .FirstOrDefaultAsync(item => item.Id == submissionId, cancellationToken);
        if (submission is null)
        {
            return ApiResults.Error("NOT_FOUND", "Submission was not found.", StatusCodes.Status404NotFound);
        }

        return ApiResults.Success(new
        {
            submission_id = submission.Id,
            session_id = submission.SessionId,
            question_id = submission.QuestionId,
            student = AuthEndpoints.ToUserDto(submission.Session!.User!),
            assessment_id = submission.Session.AssessmentId,
            assessment_title = submission.Session.Assessment!.Title,
            evaluation_status = submission.EvaluationStatus,
            submission.Score,
            max_score = submission.MaxScore,
            stdout = submission.Stdout,
            stderr = submission.Stderr,
            files = JsonDocumentSerializer.Deserialize(submission.FilesJson, new Dictionary<string, WorkspaceFileDto>()),
            submitted_at = submission.SubmittedAt
        });
    }
}
