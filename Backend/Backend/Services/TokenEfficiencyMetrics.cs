using System.Text;
using System.Text.Json;
using Backend.Domain;

namespace Backend.Services;

public sealed record TokenDensity(
    int Characters,
    int Tokens,
    double CharactersPerToken,
    double TokensPerCharacter);

public sealed record TaskTokenEfficiencyMetrics(
    TokenDensity PromptSource,
    TokenDensity Response,
    int ContextSignalsProvided,
    int RequiredContextSignals);

public sealed record TokenEfficiencyStandardStep(
    string Purpose,
    string MinimalInput,
    string PublicVerification);

public sealed record TokenEfficiencyReferenceBaseline(
    string Version,
    string Status,
    int FullInputTokens,
    int CompactInputTokens,
    int FullTotalTokens,
    int CompactTotalTokens,
    double CompressionRate,
    double CompressionRatio,
    double StructuralUtilityRetention,
    int ReferenceScore,
    string? FailureReason = null,
    TokenDensity? CompactPromptDensity = null,
    TokenDensity? CompactResponseDensity = null,
    IReadOnlyList<TokenEfficiencyStandardStep>? StandardSteps = null);

public static class TokenEfficiencyMetrics
{
    public const int RequiredContextSignalCount = 4;
    public static readonly string[] RequiredContextSignals =
    [
        "task_goal",
        "active_file_or_code_context",
        "observed_behavior_or_test_output",
        "desired_constraint_or_acceptance_condition"
    ];

    public static TaskTokenEfficiencyMetrics Calculate(IEnumerable<AiInteraction> interactions)
    {
        var interactionList = interactions.ToArray();
        var promptText = string.Concat(interactionList.Select(item => item.Message + item.ActiveFileContent));
        var responseText = string.Concat(interactionList.Select(item => item.ResponseMarkdown));
        var promptTokens = interactionList.Sum(item => item.InputTokens);
        var responseTokens = interactionList.Sum(item => item.OutputTokens);
        var providedSignals = DetectContextSignals(interactionList);

        return new TaskTokenEfficiencyMetrics(
            CalculateDensity(promptText, promptTokens),
            CalculateDensity(responseText, responseTokens),
            providedSignals.Length,
            RequiredContextSignalCount);
    }

    public static string[] DetectContextSignals(IEnumerable<AiInteraction> interactions)
    {
        var interactionList = interactions.ToArray();
        var prompts = string.Join("\n", interactionList.Select(interaction => interaction.Message)).ToLowerInvariant();
        var signals = new List<string>();
        if (interactionList.Any(interaction => CountWords(interaction.Message) >= 5))
            signals.Add("task_goal");
        if (interactionList.Any(interaction => !string.IsNullOrWhiteSpace(interaction.ActiveFileContent)))
            signals.Add("active_file_or_code_context");
        if (ContainsAny(prompts, "error", "fail", "test", "expected", "actual", "stdout", "stderr"))
            signals.Add("observed_behavior_or_test_output");
        if (ContainsAny(prompts, "must", "should", "preserve", "require", "acceptance", "constraint", "edge case", "validation"))
            signals.Add("desired_constraint_or_acceptance_condition");
        return signals.ToArray();
    }

    public static int CountCharacters(string value) => value.EnumerateRunes().Count();

    public static TokenDensity CalculateDensity(string text, int tokens)
    {
        var characters = CountCharacters(text);
        return new TokenDensity(
            characters,
            tokens,
            tokens > 0 ? Math.Round(characters / (double)tokens, 3) : 0,
            characters > 0 ? Math.Round(tokens / (double)characters, 3) : 0);
    }

    private static int CountWords(string value) =>
        value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.Ordinal));
}

public sealed class TokenEfficiencyReferenceBaselineService(AiCompletionService completionService)
{
    public const string Version = "token-efficiency-baseline-v1";
    private const int MaxOutputTokens = 240;

    public async Task<TokenEfficiencyReferenceBaseline> RunAsync(Question question, CancellationToken cancellationToken)
    {
        try
        {
            var systemPrompt = "Return a JSON object with non-empty string fields goal, code_context, observed_behavior, constraint, and a standard_steps array of 2 to 5 objects. Each step object must contain non-empty string fields purpose, minimal_input, and public_verification. Minimal input must be the smallest useful student request to an AI assistant for that step. Use only public task context. Do not provide a code solution, hidden test, or grading rule.";
            var fullPrompt = BuildFullPrompt(question);
            var compactPrompt = BuildCompactPrompt(question);
            var full = await completionService.GenerateAsync(systemPrompt, fullPrompt, AiResponseFormat.Json, cancellationToken, MaxOutputTokens);
            var compact = await completionService.GenerateAsync(systemPrompt, compactPrompt, AiResponseFormat.Json, cancellationToken, MaxOutputTokens);
            var fullCoverage = CountUtilityFields(full.Content);
            var compactCoverage = CountUtilityFields(compact.Content);
            var fullSteps = ParseStandardSteps(full.Content);
            var compactSteps = ParseStandardSteps(compact.Content);

            if (full.InputTokens <= 0 || compact.InputTokens <= 0 || fullCoverage < TokenEfficiencyMetrics.RequiredContextSignalCount || compactCoverage < TokenEfficiencyMetrics.RequiredContextSignalCount || fullSteps.Count is < 2 or > 5 || compactSteps.Count is < 2 or > 5)
            {
                return Unavailable("baseline_response_missing_required_context");
            }

            var compressionRate = Math.Round(compact.InputTokens / (double)full.InputTokens, 4);
            var compressionRatio = Math.Round(full.InputTokens / (double)compact.InputTokens, 4);
            var retention = Math.Round(Math.Min(fullCoverage, compactCoverage) / (double)TokenEfficiencyMetrics.RequiredContextSignalCount, 4);
            var score = (int)Math.Round(100 * (1 - compressionRate) * retention, MidpointRounding.AwayFromZero);
            return new TokenEfficiencyReferenceBaseline(
                Version,
                "complete",
                full.InputTokens,
                compact.InputTokens,
                full.InputTokens + full.OutputTokens,
                compact.InputTokens + compact.OutputTokens,
                compressionRate,
                compressionRatio,
                retention,
                Math.Clamp(score, 0, 100),
                CompactPromptDensity: TokenEfficiencyMetrics.CalculateDensity(compactPrompt, compact.InputTokens),
                CompactResponseDensity: TokenEfficiencyMetrics.CalculateDensity(compact.Content, compact.OutputTokens),
                StandardSteps: compactSteps);
        }
        catch (AiProviderUnavailableException exception)
        {
            return Unavailable("provider_unavailable", exception.Message);
        }
    }

    private static TokenEfficiencyReferenceBaseline Unavailable(string reason, string? detail = null) =>
        new(Version, "unavailable", 0, 0, 0, 0, 0, 0, 0, 0, detail ?? reason);

    private static string BuildFullPrompt(Question question)
    {
        var starterCode = JsonDocumentSerializer.DeserializeStarterCode(question.StarterCodeJson);
        var files = string.Join("\n", starterCode
            .OrderBy(language => language.Key)
            .SelectMany(language => language.Value.OrderBy(file => file.Key)
                .Select(file => $"[{language.Key}/{file.Key}]\n{file.Value}")));
        return $"""
        Task title: {question.Title}
        Task goal and acceptance conditions:
        {question.ProblemDescriptionMarkdown}

        Visible starter files:
        {files}

        Observed behavior: no execution has run yet.
        """;
    }

    private static string BuildCompactPrompt(Question question)
    {
        var starterCode = JsonDocumentSerializer.DeserializeStarterCode(question.StarterCodeJson);
        var fileNames = string.Join(", ", starterCode
            .OrderBy(language => language.Key)
            .SelectMany(language => language.Value.Keys.OrderBy(name => name).Select(name => $"{language.Key}/{name}")));
        return $"""
        Goal: {question.Title}. {question.ProblemDescriptionMarkdown}
        Code context: visible files are {fileNames}.
        Observed behavior: no execution has run yet.
        Constraint: preserve the stated acceptance conditions and public interfaces.
        """;
    }

    private static int CountUtilityFields(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return new[] { "goal", "code_context", "observed_behavior", "constraint" }
                .Count(name => document.RootElement.TryGetProperty(name, out var value)
                    && value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(value.GetString()));
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static IReadOnlyList<TokenEfficiencyStandardStep> ParseStandardSteps(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("standard_steps", out var steps)
                || steps.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return steps.EnumerateArray()
                .Select(step => new TokenEfficiencyStandardStep(
                    ReadRequiredString(step, "purpose"),
                    ReadRequiredString(step, "minimal_input"),
                    ReadRequiredString(step, "public_verification")))
                .Where(step => !string.IsNullOrWhiteSpace(step.Purpose)
                    && !string.IsNullOrWhiteSpace(step.MinimalInput)
                    && !string.IsNullOrWhiteSpace(step.PublicVerification))
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string ReadRequiredString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
