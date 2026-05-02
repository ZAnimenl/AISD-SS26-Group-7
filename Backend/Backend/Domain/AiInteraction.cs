namespace Backend.Domain;

public sealed class AiInteraction
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public Guid AssessmentId { get; set; }

    public Guid QuestionId { get; set; }

    public string InteractionType { get; set; } = "chat";

    public string Message { get; set; } = string.Empty;

    public string SelectedLanguage { get; set; } = "python";

    public string ActiveFileContent { get; set; } = string.Empty;

    public string ResponseMarkdown { get; set; } = string.Empty;

    public string SemanticTagsJson { get; set; } = "[]";

    public DateTimeOffset CreatedAt { get; set; }
}
