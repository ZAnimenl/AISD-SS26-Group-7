namespace Backend.Domain;

public sealed class Submission
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public AssessmentSession? Session { get; set; }

    public Guid QuestionId { get; set; }

    public string EvaluationStatus { get; set; } = ExecutionStatuses.Failed;

    public int Score { get; set; }

    public int MaxScore { get; set; } = 100;

    public string? Stdout { get; set; }

    public string? Stderr { get; set; }

    public string FilesJson { get; set; } = "{}";

    public int VisiblePassed { get; set; }

    public int VisibleFailed { get; set; }

    public int VisibleTotal { get; set; }

    public int HiddenPassed { get; set; }

    public int HiddenFailed { get; set; }

    public int HiddenTotal { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }
}
