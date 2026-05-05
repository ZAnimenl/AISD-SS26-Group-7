namespace Backend.Configuration;

public sealed class LocalLlmOptions
{
    public const string SectionName = "LocalLlm";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "http://localhost:11434/v1";

    public string Model { get; set; } = "llama3.1";

    public string? ApiKey { get; set; }

    public string SystemPrompt { get; set; } = "You are a careful coding assistant for an online coding assessment platform.";

    public double Temperature { get; set; } = 0.2;

    public int MaxTokens { get; set; } = 512;
}