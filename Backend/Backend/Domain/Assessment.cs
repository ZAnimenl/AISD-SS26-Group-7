namespace Backend.Domain;

public sealed class Assessment
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    public string Status { get; set; } = AssessmentStatuses.Draft;

    public bool AiEnabled { get; set; }

    public bool StructuredHintsEnabled { get; set; } = true;

    public bool AiCreditsEnabled { get; set; } = true;

    public bool AiRescueEnabled { get; set; } = true;

    public bool ReflectionEnabled { get; set; } = true;

    public double RescueCorrectnessProbability { get; set; } = 0.5;

    public int? AiCreditBudgetOverride { get; set; }

    public bool ReportsReleased { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public List<Question> Questions { get; set; } = [];

    public List<AssessmentSession> Sessions { get; set; } = [];
}
