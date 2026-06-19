using Backend.Api;
using Backend.Domain;
using Backend.Services;

namespace OjSharp.Tests.ApiContractTests;

public sealed class StudentDashboardTests
{
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
