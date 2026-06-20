using Backend.Domain;

namespace Backend.Services;

public sealed record AttemptScoreRow(
    Guid SessionId,
    string SessionStatus,
    string AiGradingStatus,
    int? AiUsageScore,
    int FunctionalScore,
    int FunctionalMaxScore);

public static class FinalScoreAggregation
{
    public static double AverageCompletedAiGradedFinalScore(IEnumerable<AttemptScoreRow> rows)
    {
        var finals = rows
            .GroupBy(row => row.SessionId)
            .Select(group =>
            {
                var first = group.First();
                var functionalMaximum = group.Sum(row => row.FunctionalMaxScore);
                if (first.SessionStatus != SessionStatuses.Submitted
                    || first.AiGradingStatus != AiGradingStatuses.Completed
                    || !first.AiUsageScore.HasValue
                    || functionalMaximum <= 0)
                {
                    return (double?)null;
                }

                var functional = group.Sum(row => row.FunctionalScore) * 100.0 / functionalMaximum;
                return (functional + first.AiUsageScore.Value) / 2;
            })
            .Where(score => score.HasValue)
            .Select(score => score!.Value)
            .ToList();

        return finals.Count == 0 ? 0 : finals.Average();
    }
}

