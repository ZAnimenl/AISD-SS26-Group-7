namespace Backend.Domain;

public sealed class TestCase
{
    public Guid Id { get; set; }

    public Guid QuestionId { get; set; }

    public Question? Question { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Visibility { get; set; } = TestCaseVisibilities.Public;

    public string TestCodeJson { get; set; } = "{}";
}
