using Backend.Api;
using Backend.Domain;

namespace OjSharp.Tests.ApiContractTests;

public sealed class AiUsageByQuestionTests
{
    // SPEC REQ-40: AI token usage is calculated and stored per student per task.
    [Fact]
    public void Usage_is_grouped_by_task_without_combining_tokens()
    {
        var firstQuestionId = Guid.NewGuid();
        var secondQuestionId = Guid.NewGuid();
        var usage = AiEndpoints.BuildUsageByQuestion(
        [
            Interaction(firstQuestionId, 10, 5),
            Interaction(firstQuestionId, 4, 6),
            Interaction(secondQuestionId, 8, 2)
        ]);

        Assert.Equal(2, usage.Count);
        Assert.Equal(new AiEndpoints.TaskAiUsage(2, 14, 11, 25), usage[firstQuestionId]);
        Assert.Equal(new AiEndpoints.TaskAiUsage(1, 8, 2, 10), usage[secondQuestionId]);
    }

    // SPEC REQ-35 and REQ-64: a task transcript retains the logged prompt,
    // response, token counts, and timestamp without mixing task records.
    [Fact]
    public void Transcript_contains_only_the_requested_tasks_logged_input_and_output_in_time_order()
    {
        var firstQuestionId = Guid.NewGuid();
        var secondQuestionId = Guid.NewGuid();
        var earliest = DateTimeOffset.UtcNow.AddMinutes(-1);
        var latest = DateTimeOffset.UtcNow;
        var interactions = new[]
        {
            Interaction(firstQuestionId, 3, 4, "Explain the route", "Use the visible FastAPI controller.", latest),
            Interaction(secondQuestionId, 9, 10, "Second task prompt", "Second task response", earliest),
            Interaction(firstQuestionId, 5, 6, "What failed?", "The public test reports a validation error.", earliest)
        };

        var transcript = AiEndpoints.BuildTranscript(interactions, firstQuestionId);

        Assert.Equal(2, transcript.Count);
        Assert.Equal("What failed?", transcript[0].Input);
        Assert.Equal("The public test reports a validation error.", transcript[0].Output);
        Assert.Equal(11, transcript[0].TotalTokens);
        Assert.DoesNotContain(transcript, entry => entry.Input == "Second task prompt");
        Assert.Equal("Explain the route", transcript[1].Input);
    }

    private static AiInteraction Interaction(
        Guid questionId,
        int inputTokens,
        int outputTokens,
        string message = "prompt",
        string response = "response",
        DateTimeOffset? createdAt = null)
    {
        return new AiInteraction
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            AssessmentId = Guid.NewGuid(),
            QuestionId = questionId,
            Message = message,
            ResponseMarkdown = response,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = inputTokens + outputTokens,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }
}
