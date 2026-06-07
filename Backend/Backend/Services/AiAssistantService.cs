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

public sealed record AiWorkspaceAction(
    string Type,
    string Label,
    string? TargetFile = null,
    string? Language = null,
    string? ReplacementCode = null);

public sealed record AiAssistantResult(
    string ResponseMarkdown,
    string[] SemanticTags,
    int InputTokens,
    int OutputTokens,
    AiCodeSuggestion? Suggestion,
    IReadOnlyList<AiWorkspaceAction> WorkspaceActions);

public sealed class AiAssistantService
{
    private const int MaxPromptFileCount = 8;
    private const int MaxPromptFileCharacters = 6000;
    private const int MaxRunOutputCharacters = 4000;
    private const int MaxSuggestionCharacters = 50000;
    private const int MaxActionRepairAttempts = 2;
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
            visibleStarterFiles);
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

        var totalInputTokens = result.InputTokens;
        var totalOutputTokens = result.OutputTokens;
        var assistantResult = ParseStructuredResponse(result.Content, context, tags, totalInputTokens, totalOutputTokens);
        var requestedTargets = GetExplicitFileActionTargets(context);
        for (var repairAttempt = 0; repairAttempt < MaxActionRepairAttempts; repairAttempt += 1)
        {
            var missingExplicitTargets = FindMissingExplicitFileActionTargets(requestedTargets, assistantResult);
            if (missingExplicitTargets.Length == 0)
            {
                return assistantResult;
            }

            var repairResult = await completionService.GenerateAsync(
                BuildSystemPrompt(context),
                BuildRepairUserPrompt(context, tags, assistantResult, missingExplicitTargets),
                AiResponseFormat.Json,
                cancellationToken);
            totalInputTokens += repairResult.InputTokens;
            totalOutputTokens += repairResult.OutputTokens;
            var repairedAssistantResult = ParseStructuredResponse(
                repairResult.Content,
                context,
                tags,
                totalInputTokens,
                totalOutputTokens);
            assistantResult = SelectBestActionResult(requestedTargets, assistantResult, repairedAssistantResult)
                              with { InputTokens = totalInputTokens, OutputTokens = totalOutputTokens };
        }

        return assistantResult;
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
            "- Use only visible task context and visible workspace files.",
            "- Be concise and practical.",
            "- Put student-visible guidance in the response_markdown JSON field using Markdown formatting.",
            "- If the student asks for debugging help, point them toward the bug without fixing it entirely.",
            "- For code_suggestion requests, provide bounded complete-file replacements for the visible workspace files that must change.",
            "- Complete-file replacement means the full content of that one visible file, not hidden tests or administrator-only solution material.",
            "- When the task requires coordinated edits across visible files, such as HTML markup plus JavaScript behavior, emit one replace_file action for each required visible file.",
            "- Keep replacements minimal and include no more than three replace_file actions.",
            "- Prefer the active file unless the student's request explicitly names another visible file.",
            "- For JavaScript frontend tasks, files such as index.html, style.css, and app.js are all part of the JavaScript workspace; set replace_file.language to javascript for each of them.",
            "- Preserve the starter file's required public function names, exports, imports, and rendering entry points.",
            "- Do not invent a new entry function name when the starter file already defines one.",
            "- Do not create suggestions for hidden tests, grading internals, administrator notes, or provider/system details.",
            "- If you are not confident a replacement belongs in a visible file, do not emit that replace_file action.",
            "- Use workspace_actions for direct workspace actions the UI can execute after the student confirms.",
            "- Add run_public_checks when the student asks to run/test, or when a proposed edit should be verified by public checks.",
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
              },
              "workspace_actions": [
                {
                  "type": "replace_file",
                  "target_file": "visible file name",
                  "language": "python",
                  "replacement_code": "complete replacement text for one visible file only",
                  "label": "Apply edit"
                },
                {
                  "type": "replace_file",
                  "target_file": "another visible file name",
                  "language": "python",
                  "replacement_code": "complete replacement text for one visible file only",
                  "label": "Apply edit"
                },
                {
                  "type": "run_public_checks",
                  "label": "Run public checks"
                }
              ]
            }
            """,
            "Use null for suggestion and [] for workspace_actions when the response is explanation-only or debugging guidance without a safe action.",
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

    private static string BuildRepairUserPrompt(
        AiGenerationContext context,
        string[] semanticTags,
        AiAssistantResult previousResult,
        string[] missingExplicitTargets)
    {
        return string.Join("\n",
        [
            BuildUserPrompt(context, semanticTags),
            "",
            "Structured action correction required:",
            "The student explicitly requested edits to these visible files, but the previous JSON response did not include replace_file actions for them:",
            string.Join(", ", missingExplicitTargets),
            "",
            "Previous response_markdown:",
            previousResult.ResponseMarkdown,
            "",
            "Your previous response was rejected because it did not include executable workspace actions for every explicitly requested visible file.",
            "Return corrected valid JSON only.",
            "Do not answer with prose-only instructions.",
            "Include one replace_file workspace_actions entry for each listed file, using target_file exactly as listed and complete replacement_code for that file.",
            "For JavaScript workspace files such as index.html, set language to javascript.",
            "Keep any needed run_public_checks action. Do not invent hidden files or grading details."
        ]);
    }

    private static Dictionary<string, string> NormalizeVisibleFiles(
        Dictionary<string, string>? visibleFiles,
        string activeFileName,
        string activeFileContent,
        IReadOnlyDictionary<string, string> visibleStarterFiles)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var source = visibleFiles ?? new Dictionary<string, string>();

        foreach (var (fileName, content) in source
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Take(MaxPromptFileCount))
        {
            var safeName = Path.GetFileName(fileName);
            if (IsSafeWorkspaceFileName(safeName)
                && (visibleStarterFiles.Count == 0 || visibleStarterFiles.ContainsKey(safeName)))
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
            var workspaceActions = TryReadWorkspaceActions(root, context, suggestion);
            return new AiAssistantResult(markdown.Trim(), tags, inputTokens, outputTokens, suggestion, workspaceActions);
        }
        catch (JsonException)
        {
            return new AiAssistantResult(providerContent.Trim(), fallbackTags, inputTokens, outputTokens, null, []);
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

        if (!IsVisibleWorkspaceFile(context, targetFile)
            || !IsActionLanguageCompatible(context, targetFile, language)
            || string.IsNullOrWhiteSpace(replacementCode)
            || replacementCode.Length > MaxSuggestionCharacters
            || !PreservesStarterSymbols(context, targetFile, replacementCode))
        {
            return null;
        }

        return new AiCodeSuggestion(targetFile, context.SelectedLanguage, replacementCode, applyLabel);
    }

    private static IReadOnlyList<AiWorkspaceAction> TryReadWorkspaceActions(
        JsonElement root,
        AiGenerationContext context,
        AiCodeSuggestion? suggestion)
    {
        var actions = new List<AiWorkspaceAction>();
        var replaceActionTargets = new HashSet<string>(StringComparer.Ordinal);

        if (root.TryGetProperty("workspace_actions", out var actionsElement)
            && actionsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var actionElement in actionsElement.EnumerateArray().Take(6))
            {
                if (actionElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var type = GetString(actionElement, "type") ?? "";
                if (type.Equals(AiWorkspaceActionTypes.ReplaceFile, StringComparison.Ordinal)
                    && replaceActionTargets.Count < 3
                    && TryBuildReplaceFileAction(actionElement, context, out var replaceAction))
                {
                    if (replaceAction.TargetFile is not null && replaceActionTargets.Add(replaceAction.TargetFile))
                    {
                        actions.Add(replaceAction);
                    }
                    continue;
                }

                if (type.Equals(AiWorkspaceActionTypes.RunPublicChecks, StringComparison.Ordinal))
                {
                    actions.Add(new AiWorkspaceAction(
                        AiWorkspaceActionTypes.RunPublicChecks,
                        GetString(actionElement, "label") ?? "Run public checks"));
                }
            }
        }

        if (replaceActionTargets.Count == 0 && suggestion is not null)
        {
            actions.Insert(0, new AiWorkspaceAction(
                AiWorkspaceActionTypes.ReplaceFile,
                suggestion.ApplyLabel,
                suggestion.TargetFile,
                suggestion.Language,
                suggestion.ReplacementCode));
        }

        return actions;
    }

    private static bool TryBuildReplaceFileAction(
        JsonElement actionElement,
        AiGenerationContext context,
        out AiWorkspaceAction action)
    {
        action = new AiWorkspaceAction(AiWorkspaceActionTypes.ReplaceFile, "Apply edit");
        var targetFile = Path.GetFileName(GetString(actionElement, "target_file") ?? "");
        var language = GetString(actionElement, "language") ?? "";
        var replacementCode = GetString(actionElement, "replacement_code") ?? "";
        var label = GetString(actionElement, "label") ?? $"Apply to {targetFile}";

        if (!IsVisibleWorkspaceFile(context, targetFile)
            || !IsActionLanguageCompatible(context, targetFile, language)
            || string.IsNullOrWhiteSpace(replacementCode)
            || replacementCode.Length > MaxSuggestionCharacters
            || !PreservesStarterSymbols(context, targetFile, replacementCode))
        {
            return false;
        }

        action = new AiWorkspaceAction(
            AiWorkspaceActionTypes.ReplaceFile,
            label,
            targetFile,
            context.SelectedLanguage,
            replacementCode);
        return true;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool IsVisibleWorkspaceFile(AiGenerationContext context, string targetFile)
    {
        return IsSafeWorkspaceFileName(targetFile)
            && (context.VisibleStarterFiles.Count == 0
                ? targetFile.Equals(context.ActiveFileName, StringComparison.Ordinal)
                : context.VisibleStarterFiles.ContainsKey(targetFile));
    }

    private static bool IsActionLanguageCompatible(AiGenerationContext context, string targetFile, string language)
    {
        if (language.Equals(context.SelectedLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (context.SelectedLanguage == "javascript")
        {
            var extension = Path.GetExtension(targetFile);
            return extension.Equals(".html", StringComparison.OrdinalIgnoreCase) && language.Equals("html", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".css", StringComparison.OrdinalIgnoreCase) && language.Equals("css", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".json", StringComparison.OrdinalIgnoreCase) && language.Equals("json", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string[] GetExplicitFileActionTargets(AiGenerationContext context)
    {
        if (!context.InteractionType.Equals(AiInteractionTypes.CodeSuggestion, StringComparison.Ordinal)
            || !LooksLikeEditRequest(context.Message))
        {
            return [];
        }

        var normalizedMessage = context.Message.ToLowerInvariant();
        var authoritativeTargets = context.VisibleStarterFiles.Count == 0
            ? context.VisibleFiles.Keys.Where(fileName => fileName.Equals(context.ActiveFileName, StringComparison.Ordinal))
            : context.VisibleStarterFiles.Keys;
        var requestedTargets = authoritativeTargets
            .Distinct(StringComparer.Ordinal)
            .Where(fileName => normalizedMessage.Contains(fileName.ToLowerInvariant(), StringComparison.Ordinal))
            .Where(IsSafeWorkspaceFileName)
            .Take(3)
            .ToArray();
        return requestedTargets;
    }

    private static string[] FindMissingExplicitFileActionTargets(string[] requestedTargets, AiAssistantResult result)
    {
        if (requestedTargets.Length == 0)
        {
            return [];
        }

        var replaceTargets = result.WorkspaceActions
            .Where(action => action.Type == AiWorkspaceActionTypes.ReplaceFile)
            .Select(action => action.TargetFile)
            .Where(targetFile => !string.IsNullOrWhiteSpace(targetFile))
            .ToHashSet(StringComparer.Ordinal);

        return requestedTargets
            .Where(targetFile => !replaceTargets.Contains(targetFile))
            .ToArray();
    }

    private static AiAssistantResult SelectBestActionResult(
        string[] requestedTargets,
        AiAssistantResult currentResult,
        AiAssistantResult candidateResult)
    {
        return CountCoveredRequestedTargets(requestedTargets, candidateResult) >= CountCoveredRequestedTargets(requestedTargets, currentResult)
            ? candidateResult
            : currentResult;
    }

    private static int CountCoveredRequestedTargets(string[] requestedTargets, AiAssistantResult result)
    {
        if (requestedTargets.Length == 0)
        {
            return 0;
        }

        var replaceTargets = result.WorkspaceActions
            .Where(action => action.Type == AiWorkspaceActionTypes.ReplaceFile)
            .Select(action => action.TargetFile)
            .Where(targetFile => !string.IsNullOrWhiteSpace(targetFile))
            .ToHashSet(StringComparer.Ordinal);
        return requestedTargets.Count(target => replaceTargets.Contains(target));
    }

    private static bool LooksLikeEditRequest(string message)
    {
        var normalized = message.ToLowerInvariant();
        var markers = new[]
        {
            "edit",
            "update",
            "change",
            "modify",
            "replace",
            "add",
            "wire",
            "implement",
            "fix",
            "make",
            "修改",
            "更新",
            "添加",
            "实现",
            "修复"
        };

        return markers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
    }

    private static bool IsSafeWorkspaceFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
            && fileName.Equals(Path.GetFileName(fileName), StringComparison.Ordinal)
            && !fileName.Contains("..", StringComparison.Ordinal);
    }

    private static bool PreservesStarterSymbols(AiGenerationContext context, string targetFile, string replacementCode)
    {
        if (!context.VisibleStarterFiles.TryGetValue(targetFile, out var starterContent))
        {
            return true;
        }

        if (Path.GetExtension(targetFile) is not ".py" and not ".js")
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
