using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api;

public static class AiEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/assessments/{assessmentId:guid}/ai-state", StateByAssessmentAsync);
        api.MapPost("/assessments/{assessmentId:guid}/questions/{questionId:guid}/ai/hints", HintByAssessmentAsync);
        api.MapPost("/assessments/{assessmentId:guid}/questions/{questionId:guid}/ai/chat", ChatByAssessmentAsync);
        api.MapPost("/ai/inline-completion", InlineCompletion);
        api.MapGet("/assessments/{assessmentId:guid}/ai-usage", UsageByAssessmentAsync);
        api.MapGet("/admin/assessments/{assessmentId:guid}/students/{studentId:guid}/ai-interactions", AdminInteractionsByAssessmentAsync);
    }

    private static async Task<IResult> StateByAssessmentAsync(
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

        var assessment = await dbContext.Assessments
            .Include(item => item.Questions)
            .FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        var session = await dbContext.AssessmentSessions
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

        EnsureCreditsInitialized(session, assessment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResults.Success(BuildAiState(assessment, session));
    }

    private static async Task<IResult> HintByAssessmentAsync(
        Guid assessmentId,
        Guid questionId,
        AssessmentAiHintRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        AiMockService aiMockService,
        CancellationToken cancellationToken)
    {
        var (user, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Student, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var assessment = await dbContext.Assessments
            .Include(item => item.Questions)
            .FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        if (!assessment.AiEnabled)
        {
            return ApiResults.Error("AI_DISABLED", "AI assistance is disabled for this assessment.", StatusCodes.Status403Forbidden);
        }

        if (!assessment.StructuredHintsEnabled)
        {
            return ApiResults.Error("AI_MODE_DISABLED", "Structured AI hints are disabled for this assessment.", StatusCodes.Status403Forbidden);
        }

        var question = assessment.Questions.FirstOrDefault(item => item.Id == questionId);
        if (question is null)
        {
            return ApiResults.Error("QUESTION_NOT_FOUND", "Question was not found for this assessment.", StatusCodes.Status404NotFound);
        }

        if (!TryNormalizeHintLevel(request.HintLevel, out var hintLevel))
        {
            return ApiResults.Error("INVALID_HINT_LEVEL", "Select a configured AI hint level.", StatusCodes.Status400BadRequest);
        }

        var session = await dbContext.AssessmentSessions
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

        var state = EnsureQuestionState(session, assessment, question, DateTimeOffset.UtcNow);
        var creditCost = assessment.AiCreditsEnabled ? AiHintLevels.DefaultCost(hintLevel) : 0;
        if (assessment.AiCreditsEnabled && !TryDeductCredits(state, creditCost, out var creditsRemaining))
        {
            return ApiResults.Error(
                "AI_CREDITS_EXHAUSTED",
                $"No AI credits remain for this question. Remaining: {creditsRemaining}, required: {creditCost}.",
                StatusCodes.Status409Conflict);
        }

        var message = string.IsNullOrWhiteSpace(request.Message)
            ? hintLevel.Replace("_", " ")
            : request.Message.Trim();

        var (responseMarkdown, tags) = await aiMockService.GenerateResponseAsync(
            hintLevel,
            message,
            request.SelectedLanguage,
            request.ActiveFileContent,
            cancellationToken);

        var interaction = new AiInteraction
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            AssessmentId = assessmentId,
            QuestionId = questionId,
            InteractionType = "hint",
            HintLevel = hintLevel,
            CreditCost = creditCost,
            Message = message,
            SelectedLanguage = request.SelectedLanguage,
            ActiveFileContent = request.ActiveFileContent,
            ResponseMarkdown = responseMarkdown,
            SemanticTagsJson = JsonDocumentSerializer.Serialize(tags),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.AiInteractions.Add(interaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResults.Success(new
        {
            interaction_id = interaction.Id,
            response_markdown = responseMarkdown,
            hint_level = hintLevel,
            credit_cost = creditCost,
            credits_remaining = state.AiCreditsRemaining,
            semantic_tags = tags,
            created_at = interaction.CreatedAt
        });
    }

    private static async Task<IResult> ChatByAssessmentAsync(
        Guid assessmentId,
        Guid questionId,
        AssessmentAiChatRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        AiMockService aiMockService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Student, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        return ApiResults.Error(
            "AI_MODE_DISABLED",
            "Unrestricted assessment-time chat is disabled. Use a configured structured hint level.",
            StatusCodes.Status403Forbidden);
    }

    private static IResult InlineCompletion()
    {
        return ApiResults.Error("AI_DISABLED", "Inline completion is not enabled for the MVP.", StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> UsageByAssessmentAsync(
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

        var session = await dbContext.AssessmentSessions.FirstOrDefaultAsync(
            item => item.AssessmentId == assessmentId && item.UserId == user!.Id,
            cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("ATTEMPT_NOT_FOUND", "Assessment attempt was not found.", StatusCodes.Status404NotFound);
        }

        var interactions = await dbContext.AiInteractions
            .Where(interaction => interaction.SessionId == session.Id)
            .ToListAsync(cancellationToken);

        return ApiResults.Success(new
        {
            assessment_id = assessmentId,
            total_interactions = interactions.Count,
            by_type = interactions.GroupBy(interaction => interaction.InteractionType)
                .ToDictionary(group => group.Key, group => group.Count()),
            total_credit_cost = interactions.Sum(interaction => interaction.CreditCost),
            by_hint_level = interactions
                .Where(interaction => interaction.HintLevel != null)
                .GroupBy(interaction => interaction.HintLevel!)
                .ToDictionary(group => group.Key, group => new
                {
                    count = group.Count(),
                    credit_cost = group.Sum(interaction => interaction.CreditCost)
                }),
            main_semantic_tags = interactions
                .SelectMany(interaction => JsonDocumentSerializer.Deserialize(interaction.SemanticTagsJson, Array.Empty<string>()))
                .Distinct()
                .ToArray()
        });
    }

    private static async Task<IResult> AdminInteractionsByAssessmentAsync(
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

        var session = await dbContext.AssessmentSessions.FirstOrDefaultAsync(
            item => item.AssessmentId == assessmentId && item.UserId == studentId,
            cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("ATTEMPT_NOT_FOUND", "Assessment attempt was not found.", StatusCodes.Status404NotFound);
        }

        var interactions = await dbContext.AiInteractions
            .Where(interaction => interaction.SessionId == session.Id)
            .OrderBy(interaction => interaction.CreatedAt)
            .Select(interaction => new
            {
                interaction_id = interaction.Id,
                question_id = interaction.QuestionId,
                interaction_type = interaction.InteractionType,
                hint_level = interaction.HintLevel,
                credit_cost = interaction.CreditCost,
                is_rescue = interaction.IsRescue,
                rescue_correctness_label = interaction.RescueCorrectnessLabel,
                rescue_decision = interaction.RescueDecision,
                rescue_decision_time_ms = interaction.RescueDecisionTimeMs,
                interaction.Message,
                selected_language = interaction.SelectedLanguage,
                response_markdown = interaction.ResponseMarkdown,
                semantic_tags = JsonDocumentSerializer.Deserialize(interaction.SemanticTagsJson, Array.Empty<string>()),
                created_at = interaction.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return ApiResults.Success(interactions);
    }

    internal static bool TryDeductCredits(WorkspaceQuestionState state, int creditCost, out int creditsRemaining)
    {
        creditsRemaining = state.AiCreditsRemaining ?? 0;
        if (creditCost <= 0)
        {
            return true;
        }

        if (creditsRemaining < creditCost)
        {
            return false;
        }

        state.AiCreditsRemaining = creditsRemaining - creditCost;
        creditsRemaining = state.AiCreditsRemaining.Value;
        return true;
    }

    private static object BuildAiState(Assessment assessment, AssessmentSession session)
    {
        return new
        {
            assessment_id = assessment.Id,
            ai_enabled = assessment.AiEnabled,
            ai_settings = AssessmentProjectionService.ToAiSettings(assessment),
            hint_levels = BuildHintLevelsDto(),
            rescue_chances_remaining = session.RescueChancesRemaining,
            questions = assessment.Questions
                .OrderBy(question => question.SortOrder)
                .ToDictionary(
                    question => question.Id.ToString(),
                    question =>
                    {
                        var state = session.WorkspaceStates.FirstOrDefault(item => item.QuestionId == question.Id);
                        return new
                        {
                            ai_credit_budget = AssessmentProjectionService.ResolveAiCreditBudget(assessment, question),
                            ai_credits_remaining = state?.AiCreditsRemaining
                        };
                    })
        };
    }

    private static object[] BuildHintLevelsDto()
    {
        return
        [
            new { hint_level = AiHintLevels.ConceptHint, credit_cost = AiHintLevels.DefaultCost(AiHintLevels.ConceptHint) },
            new { hint_level = AiHintLevels.StrategyHint, credit_cost = AiHintLevels.DefaultCost(AiHintLevels.StrategyHint) },
            new { hint_level = AiHintLevels.DebuggingHint, credit_cost = AiHintLevels.DefaultCost(AiHintLevels.DebuggingHint) },
            new { hint_level = AiHintLevels.PseudocodeHint, credit_cost = AiHintLevels.DefaultCost(AiHintLevels.PseudocodeHint) },
            new { hint_level = AiHintLevels.CodeLevelSuggestion, credit_cost = AiHintLevels.DefaultCost(AiHintLevels.CodeLevelSuggestion) }
        ];
    }

    private static void EnsureCreditsInitialized(AssessmentSession session, Assessment assessment)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var question in assessment.Questions)
        {
            EnsureQuestionState(session, assessment, question, now);
        }
    }

    private static WorkspaceQuestionState EnsureQuestionState(
        AssessmentSession session,
        Assessment assessment,
        Question question,
        DateTimeOffset now)
    {
        var state = session.WorkspaceStates.FirstOrDefault(item => item.QuestionId == question.Id);
        if (state is null)
        {
            var starterCode = JsonDocumentSerializer.Deserialize(question.StarterCodeJson, new Dictionary<string, string>());
            var language = starterCode.ContainsKey("python") ? "python" : starterCode.Keys.FirstOrDefault() ?? "python";
            var activeFile = GetActiveFile(language);
            state = new WorkspaceQuestionState
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
                Version = 1
            };
            session.WorkspaceStates.Add(state);
        }

        state.AiCreditsRemaining ??= AssessmentProjectionService.ResolveAiCreditBudget(assessment, question);
        return state;
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

    private static bool TryNormalizeHintLevel(string hintLevel, out string normalizedHintLevel)
    {
        normalizedHintLevel = hintLevel switch
        {
            AiHintLevels.ConceptHint => AiHintLevels.ConceptHint,
            AiHintLevels.StrategyHint => AiHintLevels.StrategyHint,
            AiHintLevels.DebuggingHint => AiHintLevels.DebuggingHint,
            AiHintLevels.PseudocodeHint => AiHintLevels.PseudocodeHint,
            AiHintLevels.CodeLevelSuggestion => AiHintLevels.CodeLevelSuggestion,
            _ => string.Empty
        };
        return normalizedHintLevel.Length > 0;
    }
}
