namespace Backend.Domain;

public sealed class Question
{
    public Guid Id { get; set; }

    public Guid AssessmentId { get; set; }

    public Assessment? Assessment { get; set; }

    public string Title { get; set; } = string.Empty;

    public string TaskType { get; set; } = TaskTypes.RestApiDevelopment;

    public string Difficulty { get; set; } = "medium";

    public string VerificationMode { get; set; } = VerificationModes.ApiResponseCheck;

    public string? StarterPrototypeReference { get; set; }

    public string ProblemDescriptionMarkdown { get; set; } = string.Empty;

    public string LanguageConstraintsJson { get; set; } = "[]";

    public string StarterCodeJson { get; set; } = "{}";

    public string StarterFilesMetadataJson { get; set; } = "{}";

    public string VerificationMetadataJson { get; set; } = "{}";

    public string GradingConfigurationJson { get; set; } = "{}";

    public string AuthoringSource { get; set; } = AuthoringSources.Manual;

    public string TraceabilityMetadataJson { get; set; } = "{}";

    public string? AdminNotes { get; set; }

    public int SortOrder { get; set; }

    public int MaxScore { get; set; } = 100;

    public List<TestCase> TestCases { get; set; } = [];
}
