using Backend.Api;
using Backend.Domain;

namespace OjSharp.Tests.ApiContractTests;

public sealed class StudentResultsTests
{
    [Fact]
    public void Results_are_grouped_by_session_with_total_score_and_question_count()
    {
        var assessmentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = new AssessmentSession
        {
            Id = sessionId,
            AssessmentId = assessmentId,
            Assessment = new Assessment
            {
                Id = assessmentId,
                Title = "Assessment",
                Questions =
                [
                    new Question { Id = Guid.NewGuid() },
                    new Question { Id = Guid.NewGuid() }
                ]
            }
        };

        var summaries = StudentEndpoints.BuildResultSummaries([
            Submission(session, score: 50, maxScore: 50, submittedAt: DateTimeOffset.UtcNow.AddMinutes(-2)),
            Submission(session, score: 25, maxScore: 50, submittedAt: DateTimeOffset.UtcNow)
        ]);

        var summary = Assert.Single(summaries);
        Assert.Equal(sessionId, summary.AttemptId);
        Assert.Equal(assessmentId, summary.AssessmentId);
        Assert.Equal("Assessment", summary.AssessmentTitle);
        Assert.Equal(75, summary.Score);
        Assert.Equal(100, summary.MaxScore);
        Assert.Equal(2, summary.QuestionCount);
        Assert.Equal(ExecutionStatuses.Failed, summary.EvaluationStatus);
    }

    private static Submission Submission(
        AssessmentSession session,
        int score,
        int maxScore,
        DateTimeOffset submittedAt)
    {
        return new Submission
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Session = session,
            QuestionId = Guid.NewGuid(),
            EvaluationStatus = score == maxScore ? ExecutionStatuses.Passed : ExecutionStatuses.Failed,
            Score = score,
            MaxScore = maxScore,
            SubmittedAt = submittedAt
        };
    }
}
