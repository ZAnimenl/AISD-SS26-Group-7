using Backend.Api;
using Backend.Domain;
using Backend.Services;

namespace OjSharp.Tests.ApiContractTests;

public sealed class StudentDashboardTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(60, true)]
    [InlineData(0, false)]
    [InlineData(-50, false)]
    public void Assessment_duration_must_be_greater_than_zero(int durationMinutes, bool expected)
    {
        Assert.Equal(expected, AssessmentEndpoints.IsValidDuration(durationMinutes));
    }

    [Fact]
    public void Available_assessments_exclude_active_and_expired_attempts()
    {
        var clock = new SessionClock();

        var available = Assessment();
        var inProgress = Assessment(Session(SessionStatuses.Active, DateTimeOffset.UtcNow.AddMinutes(30)));
        var expiredByTime = Assessment(Session(SessionStatuses.Active, DateTimeOffset.UtcNow.AddMinutes(-1)));
        var markedExpired = Assessment(Session(SessionStatuses.Expired, DateTimeOffset.UtcNow.AddMinutes(30)));
        var submittedRetake = Assessment(Session(SessionStatuses.Submitted, DateTimeOffset.UtcNow.AddMinutes(-10)));

        var count = StudentEndpoints.CountAvailableAssessments(
            [available, inProgress, expiredByTime, markedExpired, submittedRetake],
            clock);

        Assert.Equal(2, count);
    }

    [Fact]
    public void Available_assessments_exclude_non_active_assessments()
    {
        var draft = Assessment();
        draft.Status = AssessmentStatuses.Draft;
        var closed = Assessment();
        closed.Status = AssessmentStatuses.Closed;
        var scheduled = Assessment();
        scheduled.StartsAt = new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);

        var count = StudentEndpoints.CountAvailableAssessments(
            [draft, closed, scheduled],
            new SessionClock(),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(0, count);
    }

    [Fact]
    public void Available_assessments_exclude_assessments_past_their_deadline()
    {
        var now = new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);
        var expired = Assessment();
        expired.ExpiresAt = now.AddMinutes(-1);
        var open = Assessment();
        open.ExpiresAt = now.AddMinutes(1);

        var count = StudentEndpoints.CountAvailableAssessments([expired, open], new SessionClock(), now);

        Assert.Equal(1, count);
    }

    [Fact]
    public void Student_results_expose_ai_summary_and_reflection_consistency()
    {
        var assessment = Assessment();
        assessment.AiEnabled = true;
        assessment.Questions.Add(new Question { Id = Guid.NewGuid(), Title = "Task" });
        var session = Session(SessionStatuses.Submitted, DateTimeOffset.UtcNow);
        session.Assessment = assessment;
        session.AssessmentId = assessment.Id;
        session.AiUsageScore = 82;
        session.AiGradingStatus = AiGradingStatuses.Completed;
        session.AiGradingSummary = "Used AI selectively and verified suggestions.";
        session.AiGradingConfidence = "high";
        session.AiGradingDetailsJson = """{"reflection_consistency":"aligned"}""";
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Session = session,
            Score = 8,
            MaxScore = 10,
            EvaluationStatus = ExecutionStatuses.Passed,
            SubmittedAt = DateTimeOffset.UtcNow
        };

        var result = Assert.Single(StudentEndpoints.BuildResultSummaries([submission]));

        Assert.Equal("Used AI selectively and verified suggestions.", result.AiGradingSummary);
        Assert.Equal("high", result.AiGradingConfidence);
        Assert.Equal("aligned", result.AiGradingDetails["reflection_consistency"].ToString());
    }

    private static Assessment Assessment(params AssessmentSession[] sessions)
    {
        return new Assessment
        {
            Id = Guid.NewGuid(),
            Title = "Assessment",
            Status = AssessmentStatuses.Active,
            Sessions = sessions.ToList()
        };
    }

    private static AssessmentSession Session(string status, DateTimeOffset expiresAt)
    {
        return new AssessmentSession
        {
            Id = Guid.NewGuid(),
            Status = status,
            StartedAt = expiresAt.AddMinutes(-60),
            ExpiresAt = expiresAt
        };
    }
}
