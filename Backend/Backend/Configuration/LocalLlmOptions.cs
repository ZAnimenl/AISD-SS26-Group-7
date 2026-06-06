namespace Backend.Configuration;

public sealed class LocalLlmOptions
{
    public const string SectionName = "LocalLlm";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    public string SystemPrompt { get; set; } = "You are a careful coding assistant for an online coding assessment platform.";

    public double Temperature { get; set; } = 0.2;

    public int MaxTokens { get; set; } = 512;
}
