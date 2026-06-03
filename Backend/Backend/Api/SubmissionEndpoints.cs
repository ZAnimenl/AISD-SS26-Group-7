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
        api.MapPost("/assessments/{assessmentId:guid}/submit", FinalizeByAssessmentAsync);
        api.MapGet("/assessments/{assessmentId:guid}/submissions", HistoryByAssessmentAsync);
        api.MapGet("/assessments/{assessmentId:guid}/reflection", GetReflectionAsync);
        api.MapPost("/assessments/{assessmentId:guid}/reflection", SubmitReflectionAsync);
        api.MapPost("/assessments/{assessmentId:guid}/reflection/auto-submit", AutoSubmitReflectionAsync);
        api.MapGet("/admin/submissions/{submissionId:guid}", AdminDetailAsync);
    }

    private static async Task<IResult> FinalizeByAssessmentAsync(
        Guid assessmentId,
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
            .FirstOrDefaultAsync(
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

        return await FinalizeSessionAsync(session, dbContext, evaluationService, cancellationToken);
    }

    private static async Task<IResult> FinalizeSessionAsync(
        AssessmentSession session,
        OjSharpDbContext dbContext,
        CodeEvaluationService evaluationService,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var addedStates = EnsureWorkspaceStates(session, now);
        dbContext.WorkspaceQuestionStates.AddRange(addedStates);

        var allSubmissions = new List<Submission>();
        foreach (var state in session.WorkspaceStates)
        {
            var question = session.Assessment!.Questions.FirstOrDefault(item => item.Id == state.QuestionId);
            if (question is null)
            {
                continue;
            }

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
                SubmittedAt = now
            };
            allSubmissions.Add(submission);
            dbContext.Submissions.Add(submission);
        }

        UpdateSessionScoreFoundation(session, allSubmissions);
        if (session.Assessment!.ReflectionEnabled)
        {
            session.Status = SessionStatuses.ReflectionPending;
            session.ReflectionStatus = ReflectionStatuses.Pending;
            session.ReflectionStartedAt = now;
            session.ReflectionExpiresAt = now.AddMinutes(5);
        }
        else
        {
            session.Status = SessionStatuses.Submitted;
            session.CompletedAt = now;
        }
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
            reflection_required = session.ReflectionStatus == ReflectionStatuses.Pending,
            reflection_expires_at = session.ReflectionExpiresAt,
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

    private static async Task<IResult> GetReflectionAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (session, error) = await RequireStudentSessionAsync(assessmentId, httpContext, dbContext, currentUserAccessor, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        return ApiResults.Success(ToReflectionDto(session!));
    }

    private static async Task<IResult> SubmitReflectionAsync(
        Guid assessmentId,
        ReflectionRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (session, error) = await RequireStudentSessionAsync(assessmentId, httpContext, dbContext, currentUserAccessor, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        CompleteReflection(session!, request.ReflectionText, autoSubmitted: false);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(ToReflectionDto(session!));
    }

    private static async Task<IResult> AutoSubmitReflectionAsync(
        Guid assessmentId,
        ReflectionRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (session, error) = await RequireStudentSessionAsync(assessmentId, httpContext, dbContext, currentUserAccessor, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        CompleteReflection(session!, request.ReflectionText, autoSubmitted: true);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(ToReflectionDto(session!));
    }

    internal static IReadOnlyCollection<WorkspaceQuestionState> EnsureWorkspaceStates(AssessmentSession session, DateTimeOffset now)
    {
        var addedStates = new List<WorkspaceQuestionState>();
        var existingQuestionIds = session.WorkspaceStates.Select(state => state.QuestionId).ToHashSet();

        foreach (var question in session.Assessment!.Questions.Where(question => !existingQuestionIds.Contains(question.Id)))
        {
            var starterCode = JsonDocumentSerializer.Deserialize(question.StarterCodeJson, new Dictionary<string, string>());
            var language = starterCode.ContainsKey("python") ? "python" : starterCode.Keys.FirstOrDefault() ?? "python";
            var activeFile = GetActiveFile(language);
            var state = new WorkspaceQuestionState
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                QuestionId = question.Id,
                SelectedLanguage = language,
                ActiveFile = activeFile,
                FilesJson = JsonDocumentSerializer.Serialize(new Dictionary<string, WorkspaceFileDto>
                {
                    [activeFile] = new WorkspaceFileDto(language, starterCode.GetValueOrDefault(language, string.Empty))
                }),
                LastSavedAt = now,
                Version = 1,
                AiCreditsRemaining = AssessmentProjectionService.ResolveAiCreditBudget(session.Assessment!, question)
            };

            session.WorkspaceStates.Add(state);
            addedStates.Add(state);
        }

        return addedStates;
    }

    private static string GetActiveFile(string language)
    {
        return language switch
        {
            "javascript" => "main.js",
            "typescript" => "main.ts",
            _ => "main.py"
        };
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

    private static void UpdateSessionScoreFoundation(AssessmentSession session, IReadOnlyCollection<Submission> submissions)
    {
        var score = submissions.Sum(submission => submission.Score);
        var maxScore = submissions.Sum(submission => submission.MaxScore);
        session.CodeCorrectnessScore = maxScore == 0 ? 0 : (int)Math.Round(score * 100.0 / maxScore);
        session.ProcessAwareScore = session.CodeCorrectnessScore;
        session.ProcessScoreExplanationJson = JsonDocumentSerializer.Serialize(new
        {
            note = "Foundation score uses code correctness until AI usage, reflection, and critical AI judgment scoring are implemented.",
            code_correctness_weight = 100
        });
    }

    private static async Task<(AssessmentSession? Session, IResult? Error)> RequireStudentSessionAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (user, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Student, cancellationToken);
        if (error is not null)
        {
            return (null, error);
        }

        var session = await dbContext.AssessmentSessions
            .Include(item => item.Assessment)
            .FirstOrDefaultAsync(
                item => item.AssessmentId == assessmentId
                        && item.UserId == user!.Id
                        && (item.Status == SessionStatuses.ReflectionPending || item.Status == SessionStatuses.Submitted),
                cancellationToken);
        if (session is null)
        {
            return (null, ApiResults.Error("ATTEMPT_NOT_FOUND", "Assessment attempt was not found.", StatusCodes.Status404NotFound));
        }

        return (session, null);
    }

    private static void CompleteReflection(AssessmentSession session, string? reflectionText, bool autoSubmitted)
    {
        if (!session.Assessment!.ReflectionEnabled)
        {
            session.ReflectionStatus = ReflectionStatuses.NotStarted;
            return;
        }

        var now = DateTimeOffset.UtcNow;
        session.ReflectionText = reflectionText ?? session.ReflectionText;
        session.ReflectionSubmittedAt = now;
        session.ReflectionStatus = autoSubmitted ? ReflectionStatuses.AutoSubmitted : ReflectionStatuses.Submitted;
        session.Status = SessionStatuses.Submitted;
        session.CompletedAt ??= now;

        if (autoSubmitted && string.IsNullOrWhiteSpace(session.ReflectionText))
        {
            session.ReflectionUnderstandingScore = 0;
        }
    }

    private static object ToReflectionDto(AssessmentSession session)
    {
        return new
        {
            assessment_id = session.AssessmentId,
            attempt_id = session.Id,
            reflection_enabled = session.Assessment!.ReflectionEnabled,
            reflection_status = session.ReflectionStatus,
            reflection_started_at = session.ReflectionStartedAt,
            reflection_expires_at = session.ReflectionExpiresAt,
            reflection_submitted_at = session.ReflectionSubmittedAt,
            reflection_text = session.ReflectionText
        };
    }

    private static async Task<IResult> HistoryByAssessmentAsync(
        Guid assessmentId,
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

        var session = await dbContext.AssessmentSessions.FirstOrDefaultAsync(
            item => item.AssessmentId == assessmentId && item.UserId == user!.Id,
            cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("ATTEMPT_NOT_FOUND", "Assessment attempt was not found.", StatusCodes.Status404NotFound);
        }

        var query = dbContext.Submissions.Where(submission => submission.SessionId == session.Id);
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
            attempt_id = submission.SessionId,
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
