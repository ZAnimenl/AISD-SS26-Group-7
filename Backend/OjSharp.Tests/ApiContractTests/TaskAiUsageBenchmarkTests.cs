using Backend.Domain;
using Backend.Services;

namespace OjSharp.Tests.ApiContractTests;

public sealed class TaskAiUsageBenchmarkTests
{
    [Fact]
    public void Generated_task_configuration_contains_a_versioned_type_and_difficulty_benchmark()
    {
        var configuration = TaskAiUsageBenchmarkFactory.AddToConfiguration(
            new Dictionary<string, string> { ["runner"] = "automated_tests" },
            TaskTypes.RestApiDevelopment,
            "hard");
        var benchmark = TaskAiUsageBenchmarkFactory.Read(
            JsonDocumentSerializer.Serialize(configuration),
            TaskTypes.RestApiDevelopment,
            "hard");

        Assert.Equal(TaskAiUsageBenchmarkFactory.Version, benchmark.Version);
        Assert.Equal(1500, benchmark.ReferenceTotalTokens);
        Assert.Equal(4, benchmark.RecommendedInteractions);
        Assert.Equal(3, benchmark.MinimumContextSignals);
        Assert.Contains("task_goal", benchmark.RequiredContextSignals);
        Assert.Contains("active_file_or_code_context", benchmark.RequiredContextSignals);
        Assert.Equal("false", configuration["requires_student_install"]);
    }

    [Fact]
    public void Legacy_configuration_uses_the_same_deterministic_fallback_standard()
    {
        var benchmark = TaskAiUsageBenchmarkFactory.Read(
            "{}",
            TaskTypes.DatabaseQuerySchema,
            "easy");

        Assert.Equal(880, benchmark.ReferenceTotalTokens);
        Assert.Equal(2, benchmark.RecommendedInteractions);
    }
}
