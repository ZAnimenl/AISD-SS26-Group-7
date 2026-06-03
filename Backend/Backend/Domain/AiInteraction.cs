namespace Backend.Domain;

public sealed class AiInteraction
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid AssessmentId { get; set; }

    public Guid QuestionId { get; set; }

    public string InteractionType { get; set; } = "chat";

    public string? HintLevel { get; set; }

    public int CreditCost { get; set; }

    public bool IsRescue { get; set; }

    public string? RescueCorrectnessLabel { get; set; }

    public string? RescueDecision { get; set; }

    public int? RescueDecisionTimeMs { get; set; }

    public string Message { get; set; } = string.Empty;

    public string SelectedLanguage { get; set; } = "python";

    public string ActiveFileContent { get; set; } = string.Empty;

    public string ResponseMarkdown { get; set; } = string.Empty;

    public string SemanticTagsJson { get; set; } = "[]";

    public DateTimeOffset CreatedAt { get; set; }
}
