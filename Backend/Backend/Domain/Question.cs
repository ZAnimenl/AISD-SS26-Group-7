namespace Backend.Domain;

public sealed class Question
{
    public Guid Id { get; set; }

    public Guid AssessmentId { get; set; }

    public Assessment? Assessment { get; set; }

    public string Title { get; set; } = string.Empty;

    public string ProblemDescriptionMarkdown { get; set; } = string.Empty;

    public string LanguageConstraintsJson { get; set; } = "[]";

    public string StarterCodeJson { get; set; } = "{}";

    public string? AdminNotes { get; set; }

    public int SortOrder { get; set; }

    public int MaxScore { get; set; } = 100;

    public List<TestCase> TestCases { get; set; } = [];
}
