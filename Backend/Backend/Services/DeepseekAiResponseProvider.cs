using System.Text;
using Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public sealed class DeepseekAiResponseProvider : IAiResponseProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<DeepseekOptions> _options;
    private readonly ILogger<DeepseekAiResponseProvider> _logger;

    public DeepseekAiResponseProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<DeepseekOptions> options,
        ILogger<DeepseekAiResponseProvider> logger)
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
        var result = await TryGenerateWithUsageAsync(context, semanticTags, cancellationToken);
        return result?.ResponseMarkdown;
    }

    public async Task<AiProviderResult?> TryGenerateWithUsageAsync(
        AiGenerationContext context,
        string[] semanticTags,
        CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(nameof(DeepseekAiResponseProvider));
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);

            var requestBody = new
            {
                model = options.Model,
                messages = new[]
                {
                    new { role = "system", content = BuildSystemPrompt(context) },
                    new { role = "user", content = BuildUserPrompt(context, semanticTags) }
                },
                thinking = new
                {
                    type = options.ThinkingEnabled ? "enabled" : "disabled"
                },
                temperature = options.Temperature,
                max_tokens = options.MaxTokens,
                stream = false
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = new StringContent(
                    JsonDocumentSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json")
            };

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Deepseek request failed with status {StatusCode}: {Body}",
                    response.StatusCode, errorBody.Length > 200 ? errorBody[..200] : errorBody);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            // Extract response content
            if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                return null;
            }

            var firstChoice = choices[0];
            if (!firstChoice.TryGetProperty("message", out var messageElement) ||
                !messageElement.TryGetProperty("content", out var contentElement))
            {
                return null;
            }

            var content = contentElement.GetString();
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            // Extract token usage from the response
            var inputTokens = 0;
            var outputTokens = 0;
            if (document.RootElement.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var promptTokens))
                    inputTokens = promptTokens.GetInt32();
                if (usage.TryGetProperty("completion_tokens", out var completionTokens))
                    outputTokens = completionTokens.GetInt32();
            }

            _logger.LogInformation(
                "Deepseek response: {InputTokens} input tokens, {OutputTokens} output tokens.",
                inputTokens, outputTokens);

            return new AiProviderResult(content.Trim(), inputTokens, outputTokens);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Deepseek provider failed; falling back to mock AI response.");
            return null;
        }
    }

    private static string BuildSystemPrompt(AiGenerationContext context)
    {
        return string.Join("\n",
        [
            "You are an embedded AI coding assistant for a timed online coding assessment platform.",
            "The student is working on a practical development task (not an algorithmic puzzle).",
            "",
            "Rules:",
            "- NEVER provide the complete solution. Guide, explain, and suggest approaches instead.",
            "- Be concise and practical. Students have limited time.",
            "- Use Markdown formatting for readability.",
            "- If the student asks for debugging help, point them toward the bug without fixing it entirely.",
            "- If the student asks for a code suggestion, show a small example or pattern, not the full answer.",
            "- If the student asks for an explanation, break down the concept clearly.",
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
