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
        api.MapPost("/assessments/{assessmentId:guid}/questions/{questionId:guid}/ai/assist", AssistByAssessmentAsync);
        api.MapGet("/assessments/{assessmentId:guid}/questions/{questionId:guid}/ai-interactions", InteractionsByQuestionAsync);
        api.MapPost("/assessments/{assessmentId:guid}/ai-interactions/{interactionId:guid}/events", RecordEventAsync);
        api.MapGet("/assessments/{assessmentId:guid}/ai-usage", UsageByAssessmentAsync);
        api.MapGet("/admin/assessments/{assessmentId:guid}/students/{studentId:guid}/ai-interactions", AdminInteractionsByAssessmentAsync);
    }

    private static async Task<IResult> AssistByAssessmentAsync(
        Guid assessmentId,
        Guid questionId,
        AssessmentAiChatRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        AiAssistantService aiAssistantService,
        CancellationToken cancellationToken)
    {
        var (user, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Student, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var assessment = await dbContext.Assessments.FindAsync([assessmentId], cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        if (!assessment.AiEnabled)
        {
            return ApiResults.Error("AI_DISABLED", "AI assistance is disabled for this assessment.", StatusCodes.Status403Forbidden);
        }

        if (!AssessmentPolicy.IsAssessmentActive(assessment))
        {
            return ApiResults.Error("ASSESSMENT_CLOSED", "AI assistance is not available for this assessment status.", StatusCodes.Status409Conflict);
        }

        var session = await SessionQueries.FirstUnexpiredAsync(
            dbContext.AssessmentSessions.Where(
                item => item.AssessmentId == assessmentId
                        && item.UserId == user!.Id
                        && item.Status == SessionStatuses.Active),
            dbContext,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("ATTEMPT_NOT_FOUND", "Active assessment attempt was not found.", StatusCodes.Status404NotFound);
        }

        var question = await dbContext.Questions.FirstOrDefaultAsync(
            question => question.Id == questionId && question.AssessmentId == assessmentId,
            cancellationToken);
        if (question is null)
        {
            return ApiResults.Error("QUESTION_NOT_FOUND", "Question was not found for this assessment.", StatusCodes.Status404NotFound);
        }

        var selectedLanguage = AssessmentPolicy.NormalizeLanguage(request.SelectedLanguage);
        if (!AssessmentPolicy.IsStudentLanguageAllowed(question, selectedLanguage))
        {
            return ApiResults.Error(
                "LANGUAGE_NOT_ALLOWED",
                "The selected language is not allowed for this task.",
                StatusCodes.Status400BadRequest);
        }

        AiAssistantResult assistantResult;
        if (LooksLikeCompleteSolutionRequest(request.Message))
        {
            assistantResult = BuildDirectSolutionSafetyResponse();
        }
        else
        {
            try
            {
                assistantResult = await aiAssistantService.GenerateResponseAsync(
                    request.InteractionType,
                    request.Message,
                    selectedLanguage,
                    request.ActiveFileName,
                    request.ActiveFileContent,
                    request.VisibleFiles,
                    GetVisibleStarterFiles(question, selectedLanguage),
                    request.LastRunResult,
                    question.Title,
                    question.ProblemDescriptionMarkdown,
                    GetVisibleStarterFileNames(question, selectedLanguage),
                    cancellationToken);
            }
            catch (AiProviderUnavailableException exception)
            {
                return ApiResults.Error(
                    "AI_PROVIDER_UNAVAILABLE",
                    exception.Message,
                    StatusCodes.Status503ServiceUnavailable);
            }
        }

        var interaction = new AiInteraction
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            AssessmentId = assessmentId,
            QuestionId = questionId,
            InteractionType = request.InteractionType,
            Message = request.Message,
            SelectedLanguage = selectedLanguage,
            ActiveFileContent = request.ActiveFileContent,
            ResponseMarkdown = assistantResult.ResponseMarkdown,
            SemanticTagsJson = JsonDocumentSerializer.Serialize(assistantResult.SemanticTags),
            InputTokens = assistantResult.InputTokens,
            OutputTokens = assistantResult.OutputTokens,
            TotalTokens = assistantResult.InputTokens + assistantResult.OutputTokens,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.AiInteractions.Add(interaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResults.Success(new
        {
            interaction_id = interaction.Id,
            response_markdown = assistantResult.ResponseMarkdown,
            semantic_tags = assistantResult.SemanticTags,
            suggestion = assistantResult.Suggestion,
            workspace_actions = assistantResult.WorkspaceActions,
            token_usage = new
            {
                input_tokens = assistantResult.InputTokens,
                output_tokens = assistantResult.OutputTokens,
                total_tokens = assistantResult.InputTokens + assistantResult.OutputTokens
            },
            created_at = interaction.CreatedAt
        });
    }

    private static async Task<IResult> InteractionsByQuestionAsync(
        Guid assessmentId,
        Guid questionId,
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

        var interactions = await DateTimeOffsetOrdering.ToAscendingListAsync(
            dbContext.AiInteractions.Where(item =>
                item.SessionId == session.Id
                && item.AssessmentId == assessmentId
                && item.QuestionId == questionId),
            dbContext,
            item => item.CreatedAt,
            cancellationToken);

        var transcript = BuildTranscript(interactions, questionId);
        return ApiResults.Success(new
        {
            assessment_id = assessmentId,
            question_id = questionId,
            interactions = transcript.Select(item => new
            {
                interaction_id = item.InteractionId,
                interaction_type = item.InteractionType,
                input = item.Input,
                output = item.Output,
                token_usage = new
                {
                    input_tokens = item.InputTokens,
                    output_tokens = item.OutputTokens,
                    total_tokens = item.TotalTokens
                },
                created_at = item.CreatedAt
            })
        });
    }

    private static async Task<IResult> RecordEventAsync(
        Guid assessmentId,
        Guid interactionId,
        AiInteractionEventRequest request,
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

        var interaction = await dbContext.AiInteractions
            .FirstOrDefaultAsync(item =>
                item.Id == interactionId
                && item.AssessmentId == assessmentId,
                cancellationToken);
        if (interaction is null
            || !await dbContext.AssessmentSessions.AnyAsync(
                item => item.Id == interaction.SessionId && item.UserId == user!.Id,
                cancellationToken))
        {
            return ApiResults.Error("AI_INTERACTION_NOT_FOUND", "AI interaction was not found.", StatusCodes.Status404NotFound);
        }

        var allowedTypes = new[]
        {
            AiInteractionEventTypes.ResponseVisible,
            AiInteractionEventTypes.Apply,
            AiInteractionEventTypes.Edit,
            AiInteractionEventTypes.Reject,
            AiInteractionEventTypes.Dismiss,
            AiInteractionEventTypes.Undo
        };
        if (!allowedTypes.Contains(request.EventType))
        {
            return ApiResults.Error("INVALID_AI_EVENT", "AI interaction event type is not supported.", StatusCodes.Status400BadRequest);
        }

        if (request.EventType == AiInteractionEventTypes.ResponseVisible
            && await dbContext.AiInteractionEvents.AnyAsync(
                item => item.InteractionId == interactionId && item.EventType == AiInteractionEventTypes.ResponseVisible,
                cancellationToken))
        {
            return ApiResults.Success(new { interaction_id = interactionId, recorded = false });
        }

        var interactionEvent = new AiInteractionEvent
        {
            Id = Guid.NewGuid(),
            InteractionId = interactionId,
            SessionId = interaction.SessionId,
            EventType = request.EventType,
            ElapsedMilliseconds = request.ElapsedMilliseconds is null
                ? null
                : Math.Clamp(request.ElapsedMilliseconds.Value, 0, 86_400_000),
            AppliedUnchanged = request.AppliedUnchanged,
            MetadataJson = JsonDocumentSerializer.Serialize(request.Metadata ?? new Dictionary<string, string>()),
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.AiInteractionEvents.Add(interactionEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new
        {
            event_id = interactionEvent.Id,
            interaction_id = interactionId,
            recorded = true
        });
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

        var totalInputTokens = interactions.Sum(i => i.InputTokens);
        var totalOutputTokens = interactions.Sum(i => i.OutputTokens);
        var totalTokens = interactions.Sum(i => i.TotalTokens);
        var avgTokensPerInteraction = interactions.Count > 0 ? totalTokens / interactions.Count : 0;
        var interactionsByQuestion = interactions
            .GroupBy(interaction => interaction.QuestionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<AiInteraction>)group.ToArray());

        return ApiResults.Success(new
        {
            attempt_id = session.Id,
            assessment_id = assessmentId,
            total_interactions = interactions.Count,
            total_input_tokens = totalInputTokens,
            total_output_tokens = totalOutputTokens,
            total_tokens = totalTokens,
            average_tokens_per_interaction = avgTokensPerInteraction,
            by_type = interactions.GroupBy(interaction => interaction.InteractionType)
                .ToDictionary(group => group.Key, group => group.Count()),
            by_question = BuildUsageByQuestion(interactions)
                .ToDictionary(item => item.Key, item => new
                {
                    total_interactions = item.Value.TotalInteractions,
                    total_input_tokens = item.Value.TotalInputTokens,
                    total_output_tokens = item.Value.TotalOutputTokens,
                    total_tokens = item.Value.TotalTokens,
                    token_efficiency = TokenEfficiencyMetrics.Calculate(
                        interactionsByQuestion.GetValueOrDefault(item.Key, []))
                }),
            main_semantic_tags = interactions
                .SelectMany(interaction => JsonDocumentSerializer.Deserialize(interaction.SemanticTagsJson, Array.Empty<string>()))
                .Distinct()
                .ToArray()
        });
    }

    internal static IReadOnlyDictionary<Guid, TaskAiUsage> BuildUsageByQuestion(IEnumerable<AiInteraction> interactions)
    {
        return interactions
            .GroupBy(interaction => interaction.QuestionId)
            .ToDictionary(group => group.Key, group => new TaskAiUsage(
                group.Count(),
                group.Sum(interaction => interaction.InputTokens),
                group.Sum(interaction => interaction.OutputTokens),
                group.Sum(interaction => interaction.TotalTokens)));
    }

    internal static IReadOnlyList<TaskAiTranscriptEntry> BuildTranscript(
        IEnumerable<AiInteraction> interactions,
        Guid questionId)
    {
        return interactions
            .Where(interaction => interaction.QuestionId == questionId)
            .OrderBy(interaction => interaction.CreatedAt)
            .Select(interaction => new TaskAiTranscriptEntry(
                interaction.Id,
                interaction.InteractionType,
                interaction.Message,
                interaction.ResponseMarkdown,
                interaction.InputTokens,
                interaction.OutputTokens,
                interaction.TotalTokens,
                interaction.CreatedAt))
            .ToList();
    }

    internal sealed record TaskAiUsage(
        int TotalInteractions,
        int TotalInputTokens,
        int TotalOutputTokens,
        int TotalTokens);

    internal sealed record TaskAiTranscriptEntry(
        Guid InteractionId,
        string InteractionType,
        string Input,
        string Output,
        int InputTokens,
        int OutputTokens,
        int TotalTokens,
        DateTimeOffset CreatedAt);

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
                response_markdown = interaction.ResponseMarkdown,
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

        return ApiResults.Success(interactions);
    }

    private static string[] GetVisibleStarterFileNames(Question question, string selectedLanguage)
    {
        return GetVisibleStarterFiles(question, selectedLanguage).Keys.OrderBy(name => name).ToArray();
    }

    private static Dictionary<string, string> GetVisibleStarterFiles(Question question, string selectedLanguage)
    {
        var starterCode = JsonDocumentSerializer.DeserializeStarterCode(question.StarterCodeJson);
        return starterCode.TryGetValue(AssessmentPolicy.NormalizeLanguage(selectedLanguage), out var languageFiles)
            ? languageFiles.ToDictionary(item => item.Key, item => item.Value)
            : [];
    }

    private static bool LooksLikeCompleteSolutionRequest(string message)
    {
        var normalized = message.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var blockedPhrases = new[]
        {
            "complete solution",
            "full solution",
            "final answer",
            "give me the answer",
            "write all code",
            "entire solution",
            "完整代码",
            "完整答案",
            "直接给答案",
            "直接答案",
            "完整解法",
            "全部代码"
        };

        return blockedPhrases.Any(normalized.Contains);
    }

    private static AiAssistantResult BuildDirectSolutionSafetyResponse()
    {
        const string response = """
        I cannot provide a complete solution for the assessment task. I can still help you make progress: describe the failing behavior, explain the relevant concept, review a small code snippet, or suggest the next testable step.
        """;

        return new AiAssistantResult(
            response,
            ["assessment_safety", "direct_solution_request"],
            0,
            0,
            null,
            []);
    }
}
