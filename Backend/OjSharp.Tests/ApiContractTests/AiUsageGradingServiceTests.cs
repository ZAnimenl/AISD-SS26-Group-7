using Backend.Domain;
using Backend.Services;
using System.Text.Json;

namespace OjSharp.Tests.ApiContractTests;

public sealed class AiUsageGradingServiceTests
{
    [Fact]
    public void Word_count_enforces_whitespace_separated_reflection_limit()
    {
        Assert.Equal(5, AiUsageGradingService.CountWords("I verified one useful suggestion."));
        Assert.Equal(0, AiUsageGradingService.CountWords("   "));
    }

    [Fact]
    public void Rapid_unchanged_accept_deduction_is_bounded_at_eight()
    {
        var events = Enumerable.Range(0, 12).Select(_ => new AiInteractionEvent
        {
            EventType = AiInteractionEventTypes.Apply,
            AppliedUnchanged = true,
            ElapsedMilliseconds = 2500
        });

        Assert.Equal(8, AiUsageGradingService.CalculateRapidAcceptDeduction(events));
    }

    [Fact]
    public void Edited_or_slow_apply_does_not_trigger_rapid_accept_deduction()
    {
        var interactionId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var events = new[]
        {
            new AiInteractionEvent
            {
                EventType = AiInteractionEventTypes.Apply,
                AppliedUnchanged = false,
                ElapsedMilliseconds = 1000
            },
            new AiInteractionEvent
            {
                EventType = AiInteractionEventTypes.Apply,
                AppliedUnchanged = true,
                ElapsedMilliseconds = 4000
            },
            new AiInteractionEvent
            {
                InteractionId = interactionId,
                EventType = AiInteractionEventTypes.Apply,
                AppliedUnchanged = true,
                ElapsedMilliseconds = 1000,
                CreatedAt = now
            },
            new AiInteractionEvent
            {
                InteractionId = interactionId,
                EventType = AiInteractionEventTypes.Edit,
                CreatedAt = now.AddSeconds(5)
            }
        };

        Assert.Equal(0, AiUsageGradingService.CalculateRapidAcceptDeduction(events));
    }

    [Fact]
    public void Duplicate_prompts_without_action_reduce_objective_repetition_score()
    {
        var now = DateTimeOffset.UtcNow;
        var interactions = new[]
        {
            Interaction("Fix this API error please", now),
            Interaction("Please fix this API error", now.AddSeconds(10))
        };

        Assert.True(AiUsageGradingService.CalculateObjectiveRepetition(interactions, []) < 10);
    }

    [Theory]
    [InlineData("""{"evidence":[{"criterion":"prompt"}]}""", 1)]
    [InlineData("""{"evidence":{"criterion":"prompt"}}""", 1)]
    [InlineData("""{"evidence":"interaction 1"}""", 1)]
    [InlineData("""{}""", 0)]
    [InlineData("""{"evidence":42}""", 0)]
    [InlineData("""{"evidence":null}""", 0)]
    public void Evidence_shapes_are_normalized_without_object_array_failures(string fragment, int expectedCount)
    {
        using var source = JsonDocument.Parse(fragment);
        var evidence = source.RootElement.TryGetProperty("evidence", out var element)
            ? AiUsageGradingService.NormalizeEvidence(element)
            : [];

        Assert.Equal(expectedCount, evidence.Count);
    }

    [Fact]
    public void Problem_statement_copy_detection_ignores_formatting_differences()
    {
        var questionId = Guid.NewGuid();
        var interaction = Interaction(
            "Build an accessible Todo summary panel.\n\nInclude pending and completed counts, preserve the existing API contract, and verify empty-state behavior.",
            DateTimeOffset.UtcNow);
        interaction.QuestionId = questionId;

        var evidence = AiUsageGradingService.DetectProblemStatementCopy(
            [interaction],
            new Dictionary<Guid, string>
            {
                [questionId] = "Build an accessible Todo summary panel. Include pending and completed counts, preserve the existing API contract, and verify empty-state behavior."
            });

        Assert.NotNull(evidence);
        Assert.Equal(interaction.Id, evidence!.InteractionId);
    }

    private static AiInteraction Interaction(string message, DateTimeOffset createdAt)
    {
        return new AiInteraction
        {
            Id = Guid.NewGuid(),
            Message = message,
            CreatedAt = createdAt
        };
    }
}
