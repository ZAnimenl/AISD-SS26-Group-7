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
        AiMockService aiMockService,
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

        var questionExists = await dbContext.Questions.AnyAsync(
            question => question.Id == questionId && question.AssessmentId == assessmentId,
            cancellationToken);
        if (!questionExists)
        {
            return ApiResults.Error("QUESTION_NOT_FOUND", "Question was not found for this assessment.", StatusCodes.Status404NotFound);
        }

        var (responseMarkdown, tags, inputTokens, outputTokens) = await aiMockService.GenerateResponseAsync(
            request.InteractionType,
            request.Message,
            request.SelectedLanguage,
            request.ActiveFileContent,
            cancellationToken);

        var interaction = new AiInteraction
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            AssessmentId = assessmentId,
            QuestionId = questionId,
            InteractionType = request.InteractionType,
            Message = request.Message,
            SelectedLanguage = request.SelectedLanguage,
            ActiveFileContent = request.ActiveFileContent,
            ResponseMarkdown = responseMarkdown,
            SemanticTagsJson = JsonDocumentSerializer.Serialize(tags),
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = inputTokens + outputTokens,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.AiInteractions.Add(interaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResults.Success(new
        {
            interaction_id = interaction.Id,
            response_markdown = responseMarkdown,
            semantic_tags = tags,
            token_usage = new
            {
                input_tokens = inputTokens,
                output_tokens = outputTokens,
                total_tokens = inputTokens + outputTokens
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
}
