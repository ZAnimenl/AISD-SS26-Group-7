namespace Backend.Services;

public sealed record AiGenerationContext(
    string InteractionType,
    string Message,
    string SelectedLanguage,
    string ActiveFileContent);

public interface IAiResponseProvider
{
    Task<string?> TryGenerateAsync(
        AiGenerationContext context,
        string[] semanticTags,
        CancellationToken cancellationToken);
}