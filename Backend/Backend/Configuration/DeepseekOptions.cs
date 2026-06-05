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

    public int MaxTokens { get; set; } = 1024;
}
