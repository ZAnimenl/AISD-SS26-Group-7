using System.Text;
using System.Text.Json;
using Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public enum AiResponseFormat
{
    Text,
    Json
}

public sealed record AiCompletionResult(
    string Content,
    int InputTokens,
    int OutputTokens);

public sealed class AiProviderUnavailableException : Exception
{
    public AiProviderUnavailableException(string message)
        : base(message)
    {
    }
}

public sealed class AiCompletionService
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IOptionsMonitor<DeepseekOptions> deepseekOptions;
    private readonly IOptionsMonitor<LocalLlmOptions> localLlmOptions;
    private readonly ILogger<AiCompletionService> logger;

    public AiCompletionService(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<DeepseekOptions> deepseekOptions,
        IOptionsMonitor<LocalLlmOptions> localLlmOptions,
        ILogger<AiCompletionService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.deepseekOptions = deepseekOptions;
        this.localLlmOptions = localLlmOptions;
        this.logger = logger;
    }

    public async Task<AiCompletionResult> GenerateAsync(
        string systemPrompt,
        string userPrompt,
        AiResponseFormat responseFormat,
        CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        var deepseek = deepseekOptions.CurrentValue;
        if (deepseek.Enabled)
        {
            if (string.IsNullOrWhiteSpace(deepseek.ApiKey))
            {
                failures.Add("DeepSeek is enabled but Deepseek__ApiKey is missing.");
            }
            else
            {
                var result = await TryGenerateDeepseekAsync(deepseek, systemPrompt, userPrompt, responseFormat, cancellationToken);
                if (result is not null)
                {
                    return result;
                }

                failures.Add("DeepSeek did not return a usable completion with token usage.");
            }
        }

        var local = localLlmOptions.CurrentValue;
        if (local.Enabled)
        {
            var result = await TryGenerateLocalLlmAsync(local, systemPrompt, userPrompt, responseFormat, cancellationToken);
            if (result is not null)
            {
                return result;
            }

            failures.Add("Local LLM did not return a usable completion with token usage.");
        }

        var message = failures.Count == 0
            ? "No real AI provider is configured."
            : string.Join(" ", failures);
        throw new AiProviderUnavailableException(message);
    }

    private Task<AiCompletionResult?> TryGenerateDeepseekAsync(
        DeepseekOptions options,
        string systemPrompt,
        string userPrompt,
        AiResponseFormat responseFormat,
        CancellationToken cancellationToken)
    {
        var requestBody = new Dictionary<string, object?>
        {
            ["model"] = options.Model,
            ["messages"] = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            ["thinking"] = new
            {
                type = options.ThinkingEnabled ? "enabled" : "disabled"
            },
            ["temperature"] = options.Temperature,
            ["max_tokens"] = options.MaxTokens,
            ["stream"] = false
        };

        if (responseFormat == AiResponseFormat.Json)
        {
            requestBody["response_format"] = new { type = "json_object" };
        }

        return SendChatCompletionAsync(
            providerName: "DeepSeek",
            clientName: "DeepSeekCompletion",
            baseUrl: options.BaseUrl,
            apiKey: options.ApiKey,
            requestBody,
            cancellationToken);
    }

    private Task<AiCompletionResult?> TryGenerateLocalLlmAsync(
        LocalLlmOptions options,
        string systemPrompt,
        string userPrompt,
        AiResponseFormat responseFormat,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl) || string.IsNullOrWhiteSpace(options.Model))
        {
            logger.LogWarning("Local LLM is enabled but BaseUrl or Model is missing.");
            return Task.FromResult<AiCompletionResult?>(null);
        }

        var requestBody = new Dictionary<string, object?>
        {
            ["model"] = options.Model,
            ["messages"] = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            ["temperature"] = options.Temperature,
            ["max_tokens"] = options.MaxTokens,
            ["stream"] = false
        };

        if (responseFormat == AiResponseFormat.Json)
        {
            requestBody["response_format"] = new { type = "json_object" };
        }

        return SendChatCompletionAsync(
            providerName: "Local LLM",
            clientName: "LocalLlmCompletion",
            baseUrl: options.BaseUrl,
            apiKey: options.ApiKey,
            requestBody,
            cancellationToken);
    }

    private async Task<AiCompletionResult?> SendChatCompletionAsync(
        string providerName,
        string clientName,
        string baseUrl,
        string? apiKey,
        object requestBody,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(clientName);
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            }

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
                logger.LogWarning("{Provider} request failed with status {StatusCode}: {Body}",
                    providerName,
                    response.StatusCode,
                    errorBody.Length > 200 ? errorBody[..200] : errorBody);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var content = ExtractContent(document.RootElement);
            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogWarning("{Provider} returned no assistant content.", providerName);
                return null;
            }

            if (!TryExtractUsage(document.RootElement, out var inputTokens, out var outputTokens))
            {
                logger.LogWarning("{Provider} returned content without token usage.", providerName);
                return null;
            }

            return new AiCompletionResult(content.Trim(), inputTokens, outputTokens);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "{Provider} provider failed.", providerName);
            return null;
        }
    }

    private static string? ExtractContent(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            return null;
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var messageElement)
            || !messageElement.TryGetProperty("content", out var contentElement))
        {
            return null;
        }

        return contentElement.GetString();
    }

    private static bool TryExtractUsage(JsonElement root, out int inputTokens, out int outputTokens)
    {
        inputTokens = 0;
        outputTokens = 0;
        if (!root.TryGetProperty("usage", out var usage))
        {
            return false;
        }

        if (!usage.TryGetProperty("prompt_tokens", out var promptTokens)
            || !usage.TryGetProperty("completion_tokens", out var completionTokens))
        {
            return false;
        }

        inputTokens = promptTokens.GetInt32();
        outputTokens = completionTokens.GetInt32();
        return true;
    }
}
