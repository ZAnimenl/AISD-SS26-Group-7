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

    public string ReflectionText { get; set; } = string.Empty;

    public int ReflectionWordCount { get; set; }

    public DateTimeOffset? ReflectionDeadline { get; set; }

    public DateTimeOffset? ReflectionSubmittedAt { get; set; }

    public string? ReflectionSubmissionReason { get; set; }

    public string AiGradingStatus { get; set; } = AiGradingStatuses.NotRequired;

    public int? AiUsageScore { get; set; }

    public string AiGradingDetailsJson { get; set; } = "{}";

    public string? AiGradingModel { get; set; }

    public string? AiRubricVersion { get; set; }

    public string? AiGradingSummary { get; set; }

    public string? AiGradingConfidence { get; set; }

    public DateTimeOffset? AiGradedAt { get; set; }

    public List<WorkspaceQuestionState> WorkspaceStates { get; set; } = [];

    public List<Submission> Submissions { get; set; } = [];
}
