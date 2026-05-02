namespace Backend.Domain;

public sealed class WorkspaceQuestionState
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public AssessmentSession? Session { get; set; }

    public Guid QuestionId { get; set; }

    public string SelectedLanguage { get; set; } = "python";

    public string ActiveFile { get; set; } = "main.py";

    public string FilesJson { get; set; } = "{}";

    public DateTimeOffset LastSavedAt { get; set; }

    public int Version { get; set; }
}
