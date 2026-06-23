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

    [Fact]
    public void Task_benchmark_evidence_keeps_tokens_and_context_signals_separate()
    {
        var firstQuestionId = Guid.NewGuid();
        var secondQuestionId = Guid.NewGuid();
        var benchmarks = new Dictionary<Guid, TaskAiUsageBenchmark>
        {
            [firstQuestionId] = TaskAiUsageBenchmarkFactory.Create(TaskTypes.RestApiDevelopment, "medium"),
            [secondQuestionId] = TaskAiUsageBenchmarkFactory.Create(TaskTypes.BugFix, "hard")
        };
        var firstInteraction = Interaction(
            "The PUT test fails with 409. Preserve optimistic locking and validate the If-Match constraint.",
            DateTimeOffset.UtcNow);
        firstInteraction.QuestionId = firstQuestionId;
        firstInteraction.ActiveFileContent = "@app.put('/api/todos/{todo_id}')\ndef update_todo(): pass";
        firstInteraction.TotalTokens = 300;
        var secondInteraction = Interaction("Please help.", DateTimeOffset.UtcNow);
        secondInteraction.QuestionId = secondQuestionId;
        secondInteraction.TotalTokens = 900;

        var evidence = AiUsageGradingService.BuildBenchmarkEvidence(
            [firstInteraction, secondInteraction],
            benchmarks);

        var first = Assert.Single(evidence, item => item.QuestionId == firstQuestionId);
        var second = Assert.Single(evidence, item => item.QuestionId == secondQuestionId);
        Assert.Equal(300, first.TotalTokens);
        Assert.Equal(900, second.TotalTokens);
        Assert.Contains("active_file_or_code_context", first.ProvidedContextSignals);
        Assert.Contains("observed_behavior_or_test_output", first.ProvidedContextSignals);
        Assert.Contains("desired_constraint_or_acceptance_condition", first.ProvidedContextSignals);
        Assert.DoesNotContain("observed_behavior_or_test_output", second.ProvidedContextSignals);
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
