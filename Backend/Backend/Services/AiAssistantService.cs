using Backend.Domain;

namespace Backend.Services;

public sealed record AiGenerationContext(
    string InteractionType,
    string Message,
    string SelectedLanguage,
    string ActiveFileContent,
    string TaskTitle,
    string TaskDescriptionMarkdown,
    string[] VisibleStarterFileNames);

public sealed class AiAssistantService
{
    private readonly AiCompletionService completionService;

    public AiAssistantService(AiCompletionService completionService)
    {
        this.completionService = completionService;
    }

    public async Task<(string ResponseMarkdown, string[] SemanticTags, int InputTokens, int OutputTokens)> GenerateResponseAsync(
        string interactionType,
        string message,
        string selectedLanguage,
        string activeFileContent,
        string taskTitle,
        string taskDescriptionMarkdown,
        string[] visibleStarterFileNames,
        CancellationToken cancellationToken)
    {
        var tags = DeriveSemanticTags(interactionType, message, activeFileContent);
        var context = new AiGenerationContext(
            interactionType,
            message,
            selectedLanguage,
            activeFileContent,
            taskTitle,
            taskDescriptionMarkdown,
            visibleStarterFileNames);
        var result = await completionService.GenerateAsync(
            BuildSystemPrompt(context),
            BuildUserPrompt(context, tags),
            AiResponseFormat.Text,
            cancellationToken);

        return (result.Content, tags, result.InputTokens, result.OutputTokens);
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
            "",
            "Rules:",
            "- NEVER provide the complete solution.",
            "- Guide, explain, and suggest approaches using only visible task context.",
            "- Be concise and practical.",
            "- Use Markdown formatting for readability.",
            "- If the student asks for debugging help, point them toward the bug without fixing it entirely.",
            "- If the student asks for a code suggestion, show a small pattern or localized edit, not the entire task solution.",
            "",
            $"Interaction type: {context.InteractionType}",
            $"Programming language: {context.SelectedLanguage}",
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
            "Current code:",
            codeBlock,
        ]);
    }
}
