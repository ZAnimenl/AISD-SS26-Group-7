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
        api.MapPost("/executions/run", RunAsync);
        api.MapGet("/executions/{executionId:guid}", GetAsync);
    }

    private static async Task<IResult> RunAsync(
        RunCodeRequest request,
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
            item => item.Id == request.SessionId && item.UserId == user!.Id,
            cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("SESSION_NOT_FOUND", "Session was not found.", StatusCodes.Status404NotFound);
        }

        if (sessionClock.IsClosed(session))
        {
            return ApiResults.Error("SESSION_EXPIRED", "The assessment session has expired.", StatusCodes.Status409Conflict);
        }

        var publicTests = await dbContext.TestCases
            .Where(testCase => testCase.QuestionId == request.QuestionId && testCase.Visibility == TestCaseVisibilities.Public)
            .ToListAsync(cancellationToken);
        var executionId = Guid.NewGuid();
        var result = await evaluationService.EvaluateAsync(
            executionId,
            publicTests,
            request.ActiveFileContent,
            request.SelectedLanguage,
            cancellationToken);

        dbContext.ExecutionRecords.Add(new ExecutionRecord
        {
            Id = executionId,
            SessionId = request.SessionId,
            QuestionId = request.QuestionId,
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
