using System.Text;
using Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public sealed class LocalLlmAiResponseProvider : IAiResponseProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<LocalLlmOptions> _options;
    private readonly ILogger<LocalLlmAiResponseProvider> _logger;

    public LocalLlmAiResponseProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<LocalLlmOptions> options,
        ILogger<LocalLlmAiResponseProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<string?> TryGenerateAsync(
        AiGenerationContext context,
        string[] semanticTags,
        CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.BaseUrl) || string.IsNullOrWhiteSpace(options.Model))
        {
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(LocalLlmAiResponseProvider));
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");

            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
            }

            var requestBody = new
            {
                model = options.Model,
                messages = new[]
                {
                    new { role = "system", content = BuildSystemPrompt(options, context, semanticTags) },
                    new { role = "user", content = BuildUserPrompt(context, semanticTags) }
                },
                temperature = options.Temperature,
                max_tokens = options.MaxTokens,
                stream = false
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = new StringContent(JsonDocumentSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
            };

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Local LLM request failed with status {StatusCode}.", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                return null;
            }

            var firstChoice = choices[0];
            if (!firstChoice.TryGetProperty("message", out var messageElement) || !messageElement.TryGetProperty("content", out var contentElement))
            {
                return null;
            }

            var content = contentElement.GetString();
            return string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Local LLM provider failed; falling back to mock AI response.");
            return null;
        }
    }

    private static string BuildSystemPrompt(LocalLlmOptions options, AiGenerationContext context, string[] semanticTags)
    {
        var tagList = semanticTags.Length == 0 ? "none" : string.Join(", ", semanticTags);
        var basePrompt = string.IsNullOrWhiteSpace(options.SystemPrompt)
            ? "You are a careful coding assistant for a timed online coding assessment. Respond in Markdown."
            : options.SystemPrompt.Trim();

        return string.Join("\n", new[]
        {
            basePrompt,
            "",
            $"Interaction type: {context.InteractionType}",
            $"Language: {context.SelectedLanguage}",
            $"Semantic tags: {tagList}",
            "",
            "Keep the response concise, practical, and safe for an assessment setting."
        });
    }

    private static string BuildUserPrompt(AiGenerationContext context, string[] semanticTags)
    {
        var codeBlock = string.IsNullOrWhiteSpace(context.ActiveFileContent)
            ? "(no code yet)"
            : $"```{context.SelectedLanguage}\n{context.ActiveFileContent}\n```";

        var tagList = semanticTags.Length == 0 ? "none" : string.Join(", ", semanticTags);

        return string.Join("\n", new[]
        {
            $"Student request: {context.Message}",
            $"Semantic tags: {tagList}",
            "",
            "Current code:",
            codeBlock,
            "",
            "Give a helpful Markdown response. For hints and debugging, guide the student without revealing the full final solution."
        });
    }
}