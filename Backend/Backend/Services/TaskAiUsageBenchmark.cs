using System.Text.Json;
using Backend.Domain;

namespace Backend.Services;

public sealed record TaskAiUsageBenchmark(
    string Version,
    int ReferenceTotalTokens,
    int RecommendedInteractions,
    int MinimumContextSignals,
    string[] RequiredContextSignals);

public static class TaskAiUsageBenchmarkFactory
{
    public const string ConfigurationKey = "ai_usage_benchmark";
    public const string Version = "task-ai-usage-v1";

    private static readonly string[] RequiredContextSignals =
    [
        "task_goal",
        "active_file_or_code_context",
        "observed_behavior_or_test_output",
        "desired_constraint_or_acceptance_condition"
    ];

    public static TaskAiUsageBenchmark Create(string taskType, string difficulty)
    {
        var baseTokens = taskType switch
        {
            TaskTypes.FrontendUiExtension => 1000,
            TaskTypes.RestApiDevelopment => 1200,
            TaskTypes.DatabaseQuerySchema => 1100,
            TaskTypes.BugFix => 1050,
            _ => 1100
        };
        var multiplier = difficulty switch
        {
            "easy" => 0.8,
            "hard" => 1.25,
            _ => 1.0
        };
        var recommendedInteractions = difficulty switch
        {
            "easy" => 2,
            "hard" => 4,
            _ => 3
        };

        return new TaskAiUsageBenchmark(
            Version,
            (int)Math.Round(baseTokens * multiplier, MidpointRounding.AwayFromZero),
            recommendedInteractions,
            3,
            RequiredContextSignals);
    }

    public static Dictionary<string, string> AddToConfiguration(
        Dictionary<string, string>? configuration,
        string taskType,
        string difficulty)
    {
        var result = configuration is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(configuration);
        result["runner"] = result.GetValueOrDefault("runner", "automated_tests");
        result["requires_student_install"] = "false";
        result[ConfigurationKey] = JsonSerializer.Serialize(Create(taskType, difficulty));
        return result;
    }

    public static TaskAiUsageBenchmark Read(string configurationJson, string taskType, string difficulty)
    {
        try
        {
            var configuration = JsonDocumentSerializer.Deserialize(configurationJson, new Dictionary<string, string>());
            if (configuration.TryGetValue(ConfigurationKey, out var serialized)
                && JsonSerializer.Deserialize<TaskAiUsageBenchmark>(serialized) is { Version: Version } benchmark)
            {
                return benchmark;
            }
        }
        catch (JsonException)
        {
            // Legacy or malformed administrator metadata falls back to the stable standard.
        }

        return Create(taskType, difficulty);
    }
}
