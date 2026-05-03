using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api;

public static class QuestionEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/admin/assessments/{assessmentId:guid}/questions", CreateQuestionAsync);
        api.MapPut("/admin/questions/{questionId:guid}", UpdateQuestionAsync);
        api.MapDelete("/admin/questions/{questionId:guid}", DeleteQuestionAsync);
        api.MapGet("/admin/questions/{questionId:guid}/test-cases", ListTestCasesAsync);
        api.MapPost("/admin/questions/{questionId:guid}/test-cases", CreateTestCaseAsync);
        api.MapPut("/admin/test-cases/{testCaseId:guid}", UpdateTestCaseAsync);
        api.MapDelete("/admin/test-cases/{testCaseId:guid}", DeleteTestCaseAsync);
    }

    private static async Task<IResult> CreateQuestionAsync(
        Guid assessmentId,
        QuestionRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        if (!await dbContext.Assessments.AnyAsync(assessment => assessment.Id == assessmentId, cancellationToken))
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        var question = new Question
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessmentId,
            Title = request.Title,
            ProblemDescriptionMarkdown = request.ProblemDescriptionMarkdown,
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(request.LanguageConstraints),
            StarterCodeJson = JsonDocumentSerializer.Serialize(request.StarterCode),
            AdminNotes = request.AdminNotes,
            SortOrder = request.SortOrder,
            MaxScore = request.MaxScore
        };

        dbContext.Questions.Add(question);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(ToQuestionDto(question));
    }

    private static async Task<IResult> UpdateQuestionAsync(
        Guid questionId,
        QuestionRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var question = await dbContext.Questions.FindAsync([questionId], cancellationToken);
        if (question is null)
        {
            return ApiResults.Error("QUESTION_NOT_FOUND", "Question was not found.", StatusCodes.Status404NotFound);
        }

        question.Title = request.Title;
        question.ProblemDescriptionMarkdown = request.ProblemDescriptionMarkdown;
        question.LanguageConstraintsJson = JsonDocumentSerializer.Serialize(request.LanguageConstraints);
        question.StarterCodeJson = JsonDocumentSerializer.Serialize(request.StarterCode);
        question.AdminNotes = request.AdminNotes;
        question.SortOrder = request.SortOrder;
        question.MaxScore = request.MaxScore;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(ToQuestionDto(question));
    }

    private static async Task<IResult> DeleteQuestionAsync(
        Guid questionId,
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

        var question = await dbContext.Questions.FindAsync([questionId], cancellationToken);
        if (question is null)
        {
            return ApiResults.Error("QUESTION_NOT_FOUND", "Question was not found.", StatusCodes.Status404NotFound);
        }

        dbContext.Questions.Remove(question);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { question_id = questionId, deleted = true });
    }

    private static async Task<IResult> ListTestCasesAsync(
        Guid questionId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var testCases = await dbContext.TestCases
            .Where(testCase => testCase.QuestionId == questionId)
            .OrderBy(testCase => testCase.Name)
            .Select(testCase => new
            {
                test_case_id = testCase.Id,
                testCase.Name,
                testCase.Visibility,
                test_code = JsonDocumentSerializer.Deserialize(testCase.TestCodeJson, new Dictionary<string, string>())
            })
            .ToListAsync(cancellationToken);

        return ApiResults.Success(testCases);
    }

    private static async Task<IResult> CreateTestCaseAsync(
        Guid questionId,
        TestCaseRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        if (!await dbContext.Questions.AnyAsync(question => question.Id == questionId, cancellationToken))
        {
            return ApiResults.Error("QUESTION_NOT_FOUND", "Question was not found.", StatusCodes.Status404NotFound);
        }

        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            QuestionId = questionId,
            Name = request.Name,
            Visibility = NormalizeVisibility(request.Visibility),
            TestCodeJson = JsonDocumentSerializer.Serialize(NormalizeTestCode(request.TestCode))
        };

        dbContext.TestCases.Add(testCase);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { test_case_id = testCase.Id });
    }

    private static async Task<IResult> UpdateTestCaseAsync(
        Guid testCaseId,
        TestCaseRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var testCase = await dbContext.TestCases.FindAsync([testCaseId], cancellationToken);
        if (testCase is null)
        {
            return ApiResults.Error("NOT_FOUND", "Test case was not found.", StatusCodes.Status404NotFound);
        }

        testCase.Name = request.Name;
        testCase.Visibility = NormalizeVisibility(request.Visibility);
        testCase.TestCodeJson = JsonDocumentSerializer.Serialize(NormalizeTestCode(request.TestCode));

        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { test_case_id = testCase.Id });
    }

    private static async Task<IResult> DeleteTestCaseAsync(
        Guid testCaseId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var testCase = await dbContext.TestCases.FindAsync([testCaseId], cancellationToken);
        if (testCase is null)
        {
            return ApiResults.Error("NOT_FOUND", "Test case was not found.", StatusCodes.Status404NotFound);
        }

        dbContext.TestCases.Remove(testCase);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { test_case_id = testCaseId, deleted = true });
    }

    private static object ToQuestionDto(Question question)
    {
        return new
        {
            question_id = question.Id,
            question.Title,
            problem_description_markdown = question.ProblemDescriptionMarkdown,
            language_constraints = JsonDocumentSerializer.Deserialize(question.LanguageConstraintsJson, Array.Empty<string>()),
            starter_code = JsonDocumentSerializer.Deserialize(question.StarterCodeJson, new Dictionary<string, string>()),
            admin_notes = question.AdminNotes,
            sort_order = question.SortOrder,
            max_score = question.MaxScore
        };
    }

    private static string NormalizeVisibility(string visibility)
    {
        return visibility == TestCaseVisibilities.Hidden ? TestCaseVisibilities.Hidden : TestCaseVisibilities.Public;
    }

    private static Dictionary<string, string> NormalizeTestCode(Dictionary<string, string>? testCode)
    {
        return testCode is null
            ? new Dictionary<string, string>()
            : testCode.ToDictionary(item => item.Key.ToLowerInvariant(), item => item.Value);
    }
}
