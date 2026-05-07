using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api;

public static class ExecutionEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/assessments/{assessmentId:guid}/questions/{questionId:guid}/run", RunByAssessmentAsync);
        api.MapGet("/executions/{executionId:guid}", GetAsync);
    }

    private static async Task<IResult> RunByAssessmentAsync(
        Guid assessmentId,
        Guid questionId,
        AssessmentRunCodeRequest request,
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

        var session = await dbContext.AssessmentSessions.FirstOrDefaultAsync(
            item => item.AssessmentId == assessmentId
                    && item.UserId == user!.Id
                    && item.Status == SessionStatuses.Active
                    && item.ExpiresAt > DateTimeOffset.UtcNow,
            cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("ATTEMPT_NOT_FOUND", "Active assessment attempt was not found.", StatusCodes.Status404NotFound);
        }

        if (sessionClock.IsClosed(session))
        {
            return ApiResults.Error("ATTEMPT_EXPIRED", "The assessment attempt has expired.", StatusCodes.Status409Conflict);
        }

        return await RunForSessionAsync(
            session.Id,
            session.AssessmentId,
            questionId,
            request.SelectedLanguage,
            request.ActiveFileContent,
            dbContext,
            evaluationService,
            cancellationToken);
    }

    private static async Task<IResult> RunForSessionAsync(
        Guid sessionId,
        Guid assessmentId,
        Guid questionId,
        string selectedLanguage,
        string activeFileContent,
        OjSharpDbContext dbContext,
        CodeEvaluationService evaluationService,
        CancellationToken cancellationToken)
    {
        var questionExists = await dbContext.Questions.AnyAsync(
            question => question.Id == questionId && question.AssessmentId == assessmentId,
            cancellationToken);
        if (!questionExists)
        {
            return ApiResults.Error("QUESTION_NOT_FOUND", "Question was not found for this assessment.", StatusCodes.Status404NotFound);
        }

        var publicTests = await dbContext.TestCases
            .Where(testCase => testCase.QuestionId == questionId && testCase.Visibility == TestCaseVisibilities.Public)
            .ToListAsync(cancellationToken);
        var executionId = Guid.NewGuid();
        var result = await evaluationService.EvaluateAsync(
            executionId,
            publicTests,
            activeFileContent,
            selectedLanguage,
            cancellationToken);

        dbContext.ExecutionRecords.Add(new ExecutionRecord
        {
            Id = executionId,
            SessionId = sessionId,
            QuestionId = questionId,
            Status = result.Status,
            Stdout = result.Stdout,
            Stderr = result.Stderr,
            TestResultsJson = JsonDocumentSerializer.Serialize(result.TestResults.Select(testResult => new
            {
                testResult.Name,
                testResult.Visibility,
                passed = testResult.Passed,
                output = testResult.Output
            })),
            MetricsJson = JsonDocumentSerializer.Serialize(new
            {
                cpu_time_seconds = result.Metrics.CpuTimeSeconds,
                peak_memory_kb = result.Metrics.PeakMemoryKb
            }),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResults.Success(evaluationService.ToApiObject(result));
    }

    private static async Task<IResult> GetAsync(
        Guid executionId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireUserAsync(httpContext, dbContext, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var record = await dbContext.ExecutionRecords.FindAsync([executionId], cancellationToken);
        if (record is null)
        {
            return ApiResults.Error("NOT_FOUND", "Execution was not found.", StatusCodes.Status404NotFound);
        }

        return ApiResults.Success(new
        {
            execution_id = record.Id,
            record.Status,
            stdout = record.Stdout,
            stderr = record.Stderr,
            test_results = JsonDocumentSerializer.Deserialize(record.TestResultsJson, Array.Empty<object>()),
            metrics = JsonDocumentSerializer.Deserialize(record.MetricsJson, new Dictionary<string, object>())
        });
    }
}
