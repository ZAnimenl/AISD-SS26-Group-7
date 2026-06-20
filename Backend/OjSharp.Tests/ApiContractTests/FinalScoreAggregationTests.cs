using Backend.Domain;
using Backend.Services;

namespace OjSharp.Tests.ApiContractTests;

public sealed class FinalScoreAggregationTests
{
    [Fact]
    public void Average_uses_one_final_score_per_completed_ai_graded_attempt()
    {
        var completed = Guid.NewGuid();
        var pending = Guid.NewGuid();
        var rows = new[]
        {
            new AttemptScoreRow(completed, SessionStatuses.Submitted, AiGradingStatuses.Completed, 80, 20, 25),
            new AttemptScoreRow(completed, SessionStatuses.Submitted, AiGradingStatuses.Completed, 80, 25, 25),
            new AttemptScoreRow(pending, SessionStatuses.Submitted, AiGradingStatuses.Pending, null, 50, 50)
        };

        Assert.Equal(85, FinalScoreAggregation.AverageCompletedAiGradedFinalScore(rows));
    }

    [Fact]
    public void Average_excludes_registered_or_active_users_without_completed_grades()
    {
        var rows = new[]
        {
            new AttemptScoreRow(Guid.NewGuid(), SessionStatuses.Active, AiGradingStatuses.NotRequired, null, 0, 0),
            new AttemptScoreRow(Guid.NewGuid(), SessionStatuses.Submitted, AiGradingStatuses.Failed, null, 40, 50)
        };

        Assert.Equal(0, FinalScoreAggregation.AverageCompletedAiGradedFinalScore(rows));
    }
}
