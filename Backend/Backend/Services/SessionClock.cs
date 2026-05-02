using Backend.Domain;

namespace Backend.Services;

public sealed class SessionClock
{
    public DateTimeOffset UtcNow()
    {
        return DateTimeOffset.UtcNow;
    }

    public string GetEffectiveStatus(AssessmentSession session)
    {
        if (session.Status == SessionStatuses.Active && session.ExpiresAt <= UtcNow())
        {
            return SessionStatuses.Expired;
        }

        return session.Status;
    }

    public bool IsClosed(AssessmentSession session)
    {
        var status = GetEffectiveStatus(session);
        return status is SessionStatuses.Expired or SessionStatuses.Submitted or SessionStatuses.Closed;
    }
}
