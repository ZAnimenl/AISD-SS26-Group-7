namespace Backend.Domain;

public sealed class ExecutionRecord
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid QuestionId { get; set; }

    public string Status { get; set; } = ExecutionStatuses.Failed;

    public string? Stdout { get; set; }

    public string? Stderr { get; set; }

    public string TestResultsJson { get; set; } = "[]";

    public string MetricsJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
}
