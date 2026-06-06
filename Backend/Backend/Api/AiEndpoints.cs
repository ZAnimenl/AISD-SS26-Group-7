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

        (string ResponseMarkdown, string[] SemanticTags, int InputTokens, int OutputTokens) assistantResult;
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
                    request.ActiveFileContent,
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
            token_usage = new
            {
                input_tokens = assistantResult.InputTokens,
                output_tokens = assistantResult.OutputTokens,
                total_tokens = assistantResult.InputTokens + assistantResult.OutputTokens
            },
            created_at = interaction.CreatedAt
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
            .ToListAsync(cancellationToken);

        return ApiResults.Success(interactions);
    }

    private static string[] GetVisibleStarterFileNames(Question question, string selectedLanguage)
    {
        var starterCode = JsonDocumentSerializer.DeserializeStarterCode(question.StarterCodeJson);
        return starterCode.TryGetValue(AssessmentPolicy.NormalizeLanguage(selectedLanguage), out var languageFiles)
            ? languageFiles.Keys.OrderBy(name => name).ToArray()
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

    private static (string ResponseMarkdown, string[] SemanticTags, int InputTokens, int OutputTokens) BuildDirectSolutionSafetyResponse()
    {
        const string response = """
        I cannot provide a complete solution for the assessment task. I can still help you make progress: describe the failing behavior, explain the relevant concept, review a small code snippet, or suggest the next testable step.
        """;

        return (
            response,
            ["assessment_safety", "direct_solution_request"],
            0,
            0);
    }
}
