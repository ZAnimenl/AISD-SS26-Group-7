using System.Text.Json;
using Backend.Contracts;
using Backend.Domain;

namespace Backend.Services;

public sealed record AiGenerationContext(
    string InteractionType,
    string Message,
    string SelectedLanguage,
    string ActiveFileName,
    string ActiveFileContent,
    Dictionary<string, string> VisibleFiles,
    Dictionary<string, string> VisibleStarterFiles,
    AiRunContextRequest? LastRunResult,
    string TaskTitle,
    string TaskDescriptionMarkdown,
    string[] VisibleStarterFileNames);

public sealed record AiCodeSuggestion(
    string TargetFile,
    string Language,
    string ReplacementCode,
    string ApplyLabel);

public sealed record AiAssistantResult(
    string ResponseMarkdown,
    string[] SemanticTags,
    int InputTokens,
    int OutputTokens,
    AiCodeSuggestion? Suggestion);

public sealed class AiAssistantService
{
    private const int MaxPromptFileCount = 8;
    private const int MaxPromptFileCharacters = 6000;
    private const int MaxRunOutputCharacters = 4000;
    private const int MaxSuggestionCharacters = 50000;
    private readonly AiCompletionService completionService;

    public AiAssistantService(AiCompletionService completionService)
    {
        this.completionService = completionService;
    }

    public async Task<AiAssistantResult> GenerateResponseAsync(
        string interactionType,
        string message,
        string selectedLanguage,
        string? activeFileName,
        string activeFileContent,
        Dictionary<string, string>? visibleFiles,
        Dictionary<string, string> visibleStarterFiles,
        AiRunContextRequest? lastRunResult,
        string taskTitle,
        string taskDescriptionMarkdown,
        string[] visibleStarterFileNames,
        CancellationToken cancellationToken)
    {
        var tags = DeriveSemanticTags(interactionType, message, activeFileContent);
        var normalizedActiveFileName = string.IsNullOrWhiteSpace(activeFileName)
            ? InferActiveFileName(selectedLanguage, visibleStarterFileNames)
            : Path.GetFileName(activeFileName);
        var normalizedVisibleFiles = NormalizeVisibleFiles(
            visibleFiles,
            normalizedActiveFileName,
            activeFileContent,
            selectedLanguage);
        var context = new AiGenerationContext(
            interactionType,
            message,
            selectedLanguage,
            normalizedActiveFileName,
            activeFileContent,
            normalizedVisibleFiles,
            visibleStarterFiles,
            lastRunResult,
            taskTitle,
            taskDescriptionMarkdown,
            visibleStarterFileNames);
        var result = await completionService.GenerateAsync(
            BuildSystemPrompt(context),
            BuildUserPrompt(context, tags),
            AiResponseFormat.Json,
            cancellationToken);

        return ParseStructuredResponse(result.Content, context, tags, result.InputTokens, result.OutputTokens);
    }

    private static string[] DeriveSemanticTags(string interactionType, string message, string activeFileContent)
    {
        var tags = new List<string>
        {
            interactionType switch
            {
                AiInteractionTypes.CodeSuggestion => "code_suggestion",
                AiInteractionTypes.Explanation => "explanation",
                AiInteractionTypes.Debugging => "debugging",
                _ => "general_help"
            }
        };

        var combined = (message + " " + activeFileContent).ToLowerInvariant();

        if (combined.Contains("loop") || combined.Contains("for ") || combined.Contains("while "))
            tags.Add("loops");
        if (combined.Contains("recur"))
            tags.Add("recursion");
        if (combined.Contains("api") || combined.Contains("endpoint") || combined.Contains("route"))
            tags.Add("api_design");
        if (combined.Contains("bug") || combined.Contains("fix") || combined.Contains("wrong"))
            tags.Add("bug_fix");
        if (combined.Contains("exception") || combined.Contains("error") || combined.Contains("try"))
            tags.Add("error_handling");
        if (combined.Contains("list") || combined.Contains("array") || combined.Contains("dict"))
            tags.Add("data_structures");
        if (combined.Contains("database") || combined.Contains("query") || combined.Contains("sql"))
            tags.Add("database");
        if (combined.Contains("test") || combined.Contains("assert"))
            tags.Add("testing");

        return tags.Distinct().ToArray();
    }

    private static string BuildSystemPrompt(AiGenerationContext context)
    {
        return string.Join("\n",
        [
            "You are an embedded AI coding assistant for a timed online coding assessment platform.",
            "The student is working on a practical development task.",
            "Return a valid JSON object only. Do not wrap it in Markdown.",
            "",
            "Rules:",
            "- NEVER provide the complete solution.",
            "- Guide, explain, and suggest approaches using only visible task context.",
            "- Be concise and practical.",
            "- Put student-visible guidance in the response_markdown JSON field using Markdown formatting.",
            "- If the student asks for debugging help, point them toward the bug without fixing it entirely.",
            "- If a code edit is useful, provide at most one bounded replacement for the active file in suggestion.replacement_code.",
            "- Preserve the starter file's required public function names, exports, imports, and rendering entry points.",
            "- Do not invent a new entry function name when the starter file already defines one.",
            "- Do not create suggestions for hidden tests, grading internals, administrator notes, or provider/system details.",
            "- If you are not confident the replacement belongs in the active file, set suggestion to null.",
            "",
            "JSON shape:",
            """
            {
              "response_markdown": "short Markdown guidance for the student",
              "semantic_tags": ["code_suggestion"],
              "suggestion": {
                "target_file": "active file name",
                "language": "python",
                "replacement_code": "complete replacement text for the active file only",
                "apply_label": "Apply to active file"
              }
            }
            """,
            "Use null for suggestion when the response is explanation-only or debugging guidance without a safe active-file replacement.",
            "",
            $"Interaction type: {context.InteractionType}",
            $"Programming language: {context.SelectedLanguage}",
            $"Active file: {context.ActiveFileName}",
            $"Task: {context.TaskTitle}",
        ]);
    }

    private static string BuildUserPrompt(AiGenerationContext context, string[] semanticTags)
    {
        var codeBlock = string.IsNullOrWhiteSpace(context.ActiveFileContent)
            ? "(no code written yet)"
            : $"```{context.SelectedLanguage}\n{context.ActiveFileContent}\n```";

        return string.Join("\n",
        [
            $"Student request: {context.Message}",
            "",
            $"Semantic tags: {string.Join(", ", semanticTags)}",
            "",
            $"Task title: {context.TaskTitle}",
            "",
            "Task description:",
            context.TaskDescriptionMarkdown,
            "",
            "Visible starter files:",
            context.VisibleStarterFileNames.Length == 0
                ? "(none listed)"
                : string.Join(", ", context.VisibleStarterFileNames),
            "",
            "Active file content:",
            codeBlock,
            "",
            "Visible selected-language files:",
            FormatVisibleFiles(context),
            "",
            "Original visible starter files; preserve their public function names and exports:",
            FormatStarterFiles(context),
            "",
            "Latest public run feedback:",
            FormatRunContext(context.LastRunResult),
            "",
            "Remember: output valid JSON only. Include response_markdown and semantic_tags. Include suggestion only when it is a safe active-file replacement.",
        ]);
    }

    private static Dictionary<string, string> NormalizeVisibleFiles(
        Dictionary<string, string>? visibleFiles,
        string activeFileName,
        string activeFileContent,
        string selectedLanguage)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var source = visibleFiles ?? new Dictionary<string, string>();

        foreach (var (fileName, content) in source
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Take(MaxPromptFileCount))
        {
            var safeName = Path.GetFileName(fileName);
            if (IsLanguageFile(safeName, selectedLanguage))
            {
                result[safeName] = content ?? string.Empty;
            }
        }

        if (!string.IsNullOrWhiteSpace(activeFileName))
        {
            result[Path.GetFileName(activeFileName)] = activeFileContent ?? string.Empty;
        }

        return result;
    }

    private static string InferActiveFileName(string selectedLanguage, string[] visibleStarterFileNames)
    {
        var preferredExtension = selectedLanguage == "javascript" ? ".js" : ".py";
        return visibleStarterFileNames.FirstOrDefault(name => name.EndsWith(preferredExtension, StringComparison.OrdinalIgnoreCase))
            ?? (selectedLanguage == "javascript" ? "main.js" : "main.py");
    }

    private static bool IsLanguageFile(string fileName, string selectedLanguage)
    {
        var extension = Path.GetExtension(fileName);
        return selectedLanguage == "javascript"
            ? extension.Equals(".js", StringComparison.OrdinalIgnoreCase)
            : extension.Equals(".py", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatVisibleFiles(AiGenerationContext context)
    {
        if (context.VisibleFiles.Count == 0)
        {
            return "(none provided)";
        }

        return string.Join("\n\n", context.VisibleFiles.Select(file =>
        {
            var content = Truncate(file.Value, MaxPromptFileCharacters);
            return $"File: {file.Key}\n```{context.SelectedLanguage}\n{content}\n```";
        }));
    }

    private static string FormatStarterFiles(AiGenerationContext context)
    {
        if (context.VisibleStarterFiles.Count == 0)
        {
            return "(none provided)";
        }

        return string.Join("\n\n", context.VisibleStarterFiles.Select(file =>
        {
            var content = Truncate(file.Value, MaxPromptFileCharacters);
            return $"Starter file: {file.Key}\n```{context.SelectedLanguage}\n{content}\n```";
        }));
    }

    private static string FormatRunContext(AiRunContextRequest? runContext)
    {
        if (runContext is null)
        {
            return "(no public run result yet)";
        }

        var tests = runContext.TestResults is { Length: > 0 }
            ? string.Join("\n", runContext.TestResults.Select(test =>
                $"- {(test.Passed ? "PASS" : "FAIL")} {test.Name}: {Truncate(test.Output ?? "", 700)}"))
            : "(no public test details)";

        return string.Join("\n",
        [
            $"Status: {runContext.Status}",
            $"stdout: {Truncate(runContext.Stdout ?? "", MaxRunOutputCharacters)}",
            $"stderr: {Truncate(runContext.Stderr ?? "", MaxRunOutputCharacters)}",
            "Public tests:",
            tests
        ]);
    }

    private static string Truncate(string value, int maxCharacters)
    {
        if (value.Length <= maxCharacters)
        {
            return value;
        }

        return value[..maxCharacters] + "\n...[truncated visible context]";
    }

    private static AiAssistantResult ParseStructuredResponse(
        string providerContent,
        AiGenerationContext context,
        string[] fallbackTags,
        int inputTokens,
        int outputTokens)
    {
        try
        {
            using var document = JsonDocument.Parse(providerContent);
            var root = document.RootElement;
            var markdown = GetString(root, "response_markdown");
            if (string.IsNullOrWhiteSpace(markdown))
            {
                markdown = providerContent.Trim();
            }

            var tags = TryReadTags(root, fallbackTags);
        var suggestion = TryReadSuggestion(root, context);
        return new AiAssistantResult(markdown.Trim(), tags, inputTokens, outputTokens, suggestion);
        }
        catch (JsonException)
        {
            return new AiAssistantResult(providerContent.Trim(), fallbackTags, inputTokens, outputTokens, null);
        }
    }

    private static string[] TryReadTags(JsonElement root, string[] fallbackTags)
    {
        if (!root.TryGetProperty("semantic_tags", out var tagsElement) || tagsElement.ValueKind != JsonValueKind.Array)
        {
            return fallbackTags;
        }

        var tags = tagsElement
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Concat(fallbackTags)
            .Distinct()
            .ToArray();

        return tags.Length > 0 ? tags : fallbackTags;
    }

    private static AiCodeSuggestion? TryReadSuggestion(JsonElement root, AiGenerationContext context)
    {
        if (!root.TryGetProperty("suggestion", out var suggestionElement)
            || suggestionElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (suggestionElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var targetFile = Path.GetFileName(GetString(suggestionElement, "target_file") ?? "");
        var language = GetString(suggestionElement, "language") ?? "";
        var replacementCode = GetString(suggestionElement, "replacement_code") ?? "";
        var applyLabel = GetString(suggestionElement, "apply_label") ?? $"Apply to {context.ActiveFileName}";

        if (!targetFile.Equals(context.ActiveFileName, StringComparison.Ordinal)
            || !language.Equals(context.SelectedLanguage, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(replacementCode)
            || replacementCode.Length > MaxSuggestionCharacters
            || !PreservesStarterSymbols(context, replacementCode))
        {
            return null;
        }

        return new AiCodeSuggestion(targetFile, context.SelectedLanguage, replacementCode, applyLabel);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool PreservesStarterSymbols(AiGenerationContext context, string replacementCode)
    {
        if (!context.VisibleStarterFiles.TryGetValue(context.ActiveFileName, out var starterContent))
        {
            return true;
        }

        var requiredSymbols = context.SelectedLanguage == "javascript"
            ? ExtractJavaScriptSymbols(starterContent)
            : ExtractPythonFunctions(starterContent);

        return requiredSymbols.All(symbol => ContainsSymbolDefinition(replacementCode, context.SelectedLanguage, symbol));
    }

    private static string[] ExtractPythonFunctions(string code)
    {
        return code.Split('\n')
            .Select(line => line.TrimStart())
            .Where(line => line.StartsWith("def ", StringComparison.Ordinal))
            .Select(line => line[4..].Split('(')[0].Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .ToArray();
    }

    private static string[] ExtractJavaScriptSymbols(string code)
    {
        var symbols = new List<string>();
        foreach (var line in code.Split('\n').Select(item => item.Trim()))
        {
            if (line.StartsWith("function ", StringComparison.Ordinal))
            {
                var name = line["function ".Length..].Split('(')[0].Trim();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    symbols.Add(name);
                }
            }

            if (line.Contains("module.exports", StringComparison.Ordinal)
                && line.Contains('{', StringComparison.Ordinal)
                && line.Contains('}', StringComparison.Ordinal))
            {
                var exported = line[(line.IndexOf('{') + 1)..line.LastIndexOf('}')];
                symbols.AddRange(exported
                    .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => item.Split(':')[0].Trim()));
            }
        }

        return symbols.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct().ToArray();
    }

    private static bool ContainsSymbolDefinition(string code, string selectedLanguage, string symbol)
    {
        return selectedLanguage == "javascript"
            ? code.Contains($"function {symbol}", StringComparison.Ordinal)
              || code.Contains($"const {symbol}", StringComparison.Ordinal)
              || code.Contains($"let {symbol}", StringComparison.Ordinal)
              || code.Contains($"{symbol}:", StringComparison.Ordinal)
              || code.Contains($" {symbol} ", StringComparison.Ordinal)
            : code.Contains($"def {symbol}(", StringComparison.Ordinal);
    }
}
