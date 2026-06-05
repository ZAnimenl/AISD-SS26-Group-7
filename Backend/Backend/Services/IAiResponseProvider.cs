namespace Backend.Services;

public sealed record AiGenerationContext(
    string InteractionType,
    string Message,
    string SelectedLanguage,
    string ActiveFileContent,
    string TaskTitle,
    string TaskDescriptionMarkdown,
    string[] VisibleStarterFileNames);

public sealed record AiProviderResult(
    string ResponseMarkdown,
    int InputTokens,
    int OutputTokens);

public interface IAiResponseProvider
{
    Task<string?> TryGenerateAsync(
        AiGenerationContext context,
        string[] semanticTags,
        CancellationToken cancellationToken);

    Task<AiProviderResult?> TryGenerateWithUsageAsync(
        AiGenerationContext context,
        string[] semanticTags,
        CancellationToken cancellationToken)
    {
        // Default implementation for providers that do not track tokens.
        return Task.FromResult<AiProviderResult?>(null);
    }
}
