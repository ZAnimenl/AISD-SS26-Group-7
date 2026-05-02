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
        api.MapPost("/ai/chat", ChatAsync);
        api.MapPost("/ai/inline-completion", InlineCompletion);
        api.MapGet("/sessions/{sessionId:guid}/ai-usage", UsageAsync);
        api.MapGet("/admin/sessions/{sessionId:guid}/ai-interactions", AdminInteractionsAsync);
    }

    private static async Task<IResult> ChatAsync(
        AiChatRequest request,
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

        var assessment = await dbContext.Assessments.FindAsync([request.AssessmentId], cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        if (!assessment.AiEnabled)
        {
            return ApiResults.Error("AI_DISABLED", "AI assistance is disabled for this assessment.", StatusCodes.Status403Forbidden);
        }

        var sessionExists = await dbContext.AssessmentSessions.AnyAsync(
            session => session.Id == request.SessionId && session.UserId == user!.Id,
            cancellationToken);
        if (!sessionExists)
        {
            return ApiResults.Error("SESSION_NOT_FOUND", "Session was not found.", StatusCodes.Status404NotFound);
        }

        var tags = new[] { request.InteractionType == "debug" ? "debug" : "conceptual_hint" };
        var response = request.InteractionType switch
        {
            "debug" => "Review the failing branch first. Compare the sample input with the value your function returns.",
            "code_review" => "Check that the function signature matches the starter code and that edge cases return a value.",
            "explain" => "Break the problem into input parsing, transformation, and output formatting.",
            _ => "Start with the public sample and write the smallest function that satisfies it."
        };

        var interaction = new AiInteraction
        {
            Id = Guid.NewGuid(),
            SessionId = request.SessionId,
            AssessmentId = request.AssessmentId,
            QuestionId = request.QuestionId,
            InteractionType = request.InteractionType,
            Message = request.Message,
            SelectedLanguage = request.SelectedLanguage,
            ActiveFileContent = request.ActiveFileContent,
            ResponseMarkdown = response,
            SemanticTagsJson = JsonDocumentSerializer.Serialize(tags),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.AiInteractions.Add(interaction);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResults.Success(new
        {
            interaction_id = interaction.Id,
            response_markdown = response,
            semantic_tags = tags,
            created_at = interaction.CreatedAt
        });
    }

    private static IResult InlineCompletion()
    {
        return ApiResults.Error("AI_DISABLED", "Inline completion is not enabled for the MVP.", StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> UsageAsync(
        Guid sessionId,
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

        var sessionExists = await dbContext.AssessmentSessions.AnyAsync(
            session => session.Id == sessionId && session.UserId == user!.Id,
            cancellationToken);
        if (!sessionExists)
        {
            return ApiResults.Error("SESSION_NOT_FOUND", "Session was not found.", StatusCodes.Status404NotFound);
        }

        var interactions = await dbContext.AiInteractions
            .Where(interaction => interaction.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        return ApiResults.Success(new
        {
            session_id = sessionId,
            total_interactions = interactions.Count,
            by_type = interactions.GroupBy(interaction => interaction.InteractionType)
                .ToDictionary(group => group.Key, group => group.Count()),
            main_semantic_tags = interactions
                .SelectMany(interaction => JsonDocumentSerializer.Deserialize(interaction.SemanticTagsJson, Array.Empty<string>()))
                .Distinct()
                .ToArray()
        });
    }

    private static async Task<IResult> AdminInteractionsAsync(
        Guid sessionId,
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

        var interactions = await dbContext.AiInteractions
            .Where(interaction => interaction.SessionId == sessionId)
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
                created_at = interaction.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return ApiResults.Success(interactions);
    }
}
