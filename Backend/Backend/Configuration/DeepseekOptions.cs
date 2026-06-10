namespace Backend.Configuration;

public sealed class DeepseekOptions
{
    public const string SectionName = "Deepseek";

    public bool Enabled { get; set; }

    public string BaseUrl { get; set; } = "https://api.deepseek.com";

    public string Model { get; set; } = "deepseek-v4-flash";

    public string ApiKey { get; set; } = string.Empty;

    public bool ThinkingEnabled { get; set; }

    public double Temperature { get; set; } = 0.3;

    // 1024 was not enough to hold a full replacement_code in the assistant's
    // structured JSON response — the model truncated mid-string, JsonDocument
    // failed to parse, and the raw JSON leaked into the chat panel. 4096 covers
    // the longest single-file replacements we have observed so far.
    public int MaxTokens { get; set; } = 4096;
}
