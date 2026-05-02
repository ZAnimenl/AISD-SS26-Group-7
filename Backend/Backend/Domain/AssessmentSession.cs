namespace Backend.Domain;

public sealed class AssessmentSession
{
    public Guid Id { get; set; }

    public Guid AssessmentId { get; set; }

    public Assessment? Assessment { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public string Status { get; set; } = SessionStatuses.Active;

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public List<WorkspaceQuestionState> WorkspaceStates { get; set; } = [];

    public List<Submission> Submissions { get; set; } = [];
}
