namespace Backend.Domain;

public sealed class TestCase
{
    public Guid Id { get; set; }

    public Guid QuestionId { get; set; }

    public Question? Question { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Visibility { get; set; } = TestCaseVisibilities.Public;

    public string TestCodeJson { get; set; } = "{}";

    public string AuthoringSource { get; set; } = AuthoringSources.Manual;

    public string TraceabilityMetadataJson { get; set; } = "{}";

    public string PublicMetadataJson { get; set; } = "{}";

    public string AdminMetadataJson { get; set; } = "{}";
}
