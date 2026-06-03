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

    public int RescueChancesRemaining { get; set; } = 4;

    public string ReflectionStatus { get; set; } = ReflectionStatuses.NotStarted;

    public DateTimeOffset? ReflectionStartedAt { get; set; }

    public DateTimeOffset? ReflectionExpiresAt { get; set; }

    public DateTimeOffset? ReflectionSubmittedAt { get; set; }

    public string? ReflectionText { get; set; }

    public string? ReflectionEvaluationJson { get; set; }

    public int? CodeCorrectnessScore { get; set; }

    public int? AiUsageQualityScore { get; set; }

    public int? ReflectionUnderstandingScore { get; set; }

    public int? CriticalAiJudgmentScore { get; set; }

    public int? ProcessAwareScore { get; set; }

    public string? ProcessScoreExplanationJson { get; set; }

    public List<WorkspaceQuestionState> WorkspaceStates { get; set; } = [];

    public List<Submission> Submissions { get; set; } = [];
}
