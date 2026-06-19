using Backend.Api;
using Backend.Domain;
using Backend.Services;

namespace OjSharp.Tests.ApiContractTests;

public sealed class ReportAiUsageSummaryTests
{
    [Fact]
    public void Ai_usage_summary_includes_token_totals_per_task_and_efficiency()
    {
        var firstQuestionId = Guid.NewGuid();
        var secondQuestionId = Guid.NewGuid();
        var questions = new Dictionary<Guid, Question>
        {
            [firstQuestionId] = new()
            {
                Id = firstQuestionId,
                Title = "Add summary panel",
                TaskType = TaskTypes.FrontendUiExtension,
                SortOrder = 1
            },
            [secondQuestionId] = new()
            {
                Id = secondQuestionId,
                Title = "Fix API route",
                TaskType = TaskTypes.RestApiDevelopment,
                SortOrder = 2
            }
        };

        var summary = ReportEndpoints.BuildAiUsageSummary(
            [
                Interaction(firstQuestionId, inputTokens: 100, outputTokens: 60, "code_suggestion", "frontend"),
                Interaction(firstQuestionId, inputTokens: 80, outputTokens: 40, "debugging", "frontend"),
                Interaction(secondQuestionId, inputTokens: 50, outputTokens: 30, "debugging", "api")
            ],
            questions);

        Assert.Equal(3, summary.TotalInteractions);
        Assert.Equal(230, summary.TotalInputTokens);
        Assert.Equal(130, summary.TotalOutputTokens);
        Assert.Equal(360, summary.TotalTokens);
        Assert.Equal(120, summary.AverageTokensPerInteraction);
        Assert.Contains("debugging", summary.MainSemanticTags);

        Assert.Collection(
            summary.PerTaskTokenTotals,
            first =>
            {
                Assert.Equal(firstQuestionId, first.QuestionId);
                Assert.Equal("Add summary panel", first.TaskTitle);
                Assert.Equal(TaskTypes.FrontendUiExtension, first.TaskType);
                Assert.Equal(2, first.InteractionCount);
                Assert.Equal(280, first.TotalTokens);
            },
            second =>
            {
                Assert.Equal(secondQuestionId, second.QuestionId);
                Assert.Equal("Fix API route", second.TaskTitle);
                Assert.Equal(80, second.TotalTokens);
            });
    }

    private static AiInteraction Interaction(
        Guid questionId,
        int inputTokens,
        int outputTokens,
        params string[] tags)
    {
        return new AiInteraction
        {
            Id = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            AssessmentId = Guid.NewGuid(),
            QuestionId = questionId,
            SemanticTagsJson = JsonDocumentSerializer.Serialize(tags),
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = inputTokens + outputTokens
        };
    }
}
