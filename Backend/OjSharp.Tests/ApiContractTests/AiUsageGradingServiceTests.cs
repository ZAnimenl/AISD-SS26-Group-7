using Backend.Domain;
using Backend.Services;

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
