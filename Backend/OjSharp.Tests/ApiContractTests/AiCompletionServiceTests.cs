using Backend.Configuration;
using Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;
using System.Text.Json;

namespace OjSharp.Tests.ApiContractTests;

public sealed class AiCompletionServiceTests
{
    [Fact]
    public async Task Generate_async_fails_closed_when_no_real_provider_is_configured()
    {
        var service = new AiCompletionService(
            new EmptyHttpClientFactory(),
            new StaticOptionsMonitor<DeepseekOptions>(new DeepseekOptions { Enabled = false }),
            new StaticOptionsMonitor<LocalLlmOptions>(new LocalLlmOptions { Enabled = false }),
            NullLogger<AiCompletionService>.Instance);

        var exception = await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            service.GenerateAsync(
                "system prompt",
                "student prompt",
                AiResponseFormat.Text,
                CancellationToken.None));

        Assert.Equal("No real AI provider is configured.", exception.Message);
    }

    [Fact]
    public async Task Generate_async_omits_response_format_for_text_completions()
    {
        var handler = new CapturingHandler();
        var service = CreateLocalLlmService(handler);

        var result = await service.GenerateAsync(
            "system prompt",
            "student prompt",
            AiResponseFormat.Text,
            CancellationToken.None);

        using var document = JsonDocument.Parse(handler.CapturedBody);
        Assert.Equal("provider response", result.Content);
        Assert.False(document.RootElement.TryGetProperty("response_format", out _));
    }

    [Fact]
    public async Task Generate_async_sends_response_format_for_json_completions()
    {
        var handler = new CapturingHandler();
        var service = CreateLocalLlmService(handler);

        await service.GenerateAsync(
            "system prompt",
            "student prompt",
            AiResponseFormat.Json,
            CancellationToken.None);

        using var document = JsonDocument.Parse(handler.CapturedBody);
        var responseFormat = document.RootElement.GetProperty("response_format");
        Assert.Equal("json_object", responseFormat.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Generate_async_preserves_finish_reason_and_overrides_max_tokens()
    {
        var handler = new CapturingHandler(
            """
            {
              "choices": [
                {
                  "finish_reason": "length",
                  "message": {
                    "content": "{\"tasks\":"
                  }
                }
              ],
              "usage": {
                "prompt_tokens": 7,
                "completion_tokens": 9
              }
            }
            """);
        var service = CreateLocalLlmService(handler);

        var result = await service.GenerateAsync(
            "system prompt",
            "student prompt",
            AiResponseFormat.Json,
            CancellationToken.None,
            maxTokens: 4096);

        using var document = JsonDocument.Parse(handler.CapturedBody);
        Assert.Equal(4096, document.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal("length", result.FinishReason);
    }

    [Fact]
    public async Task Generate_async_reports_provider_http_error_body()
    {
        var handler = new CapturingHandler(
            """
            {"error":{"message":"invalid model"}}
            """,
            HttpStatusCode.BadRequest);
        var service = CreateLocalLlmService(handler);

        var exception = await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            service.GenerateAsync(
                "system prompt",
                "student prompt",
                AiResponseFormat.Json,
                CancellationToken.None));

        Assert.Contains("Local LLM request failed with status 400 BadRequest.", exception.Message);
        Assert.Contains("invalid model", exception.Message);
    }

    [Fact]
    public async Task Generate_async_reports_provider_network_failure()
    {
        var service = CreateLocalLlmService(new CapturingHandler(exception: new HttpRequestException("synthetic network failure")));

        var exception = await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            service.GenerateAsync(
                "system prompt",
                "student prompt",
                AiResponseFormat.Json,
                CancellationToken.None));

        Assert.Equal("Local LLM request failed: synthetic network failure", exception.Message);
    }

    [Fact]
    public async Task Generate_async_reports_missing_token_usage_specifically()
    {
        var handler = new CapturingHandler(
            """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"tasks\":[]}"
                  }
                }
              ]
            }
            """);
        var service = CreateLocalLlmService(handler);

        var exception = await Assert.ThrowsAsync<AiProviderUnavailableException>(() =>
            service.GenerateAsync(
                "system prompt",
                "student prompt",
                AiResponseFormat.Json,
                CancellationToken.None));

        Assert.Equal("Local LLM returned content without token usage.", exception.Message);
    }

    private static AiCompletionService CreateLocalLlmService(CapturingHandler handler)
    {
        return new AiCompletionService(
            new SingleClientFactory(new HttpClient(handler)),
            new StaticOptionsMonitor<DeepseekOptions>(new DeepseekOptions { Enabled = false }),
            new StaticOptionsMonitor<LocalLlmOptions>(new LocalLlmOptions
            {
                Enabled = true,
                BaseUrl = "http://local-llm.test",
                Model = "test-model"
            }),
            NullLogger<AiCompletionService>.Instance);
    }

    private sealed class EmptyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpClient client;

        public SingleClientFactory(HttpClient client)
        {
            this.client = client;
        }

        public HttpClient CreateClient(string name)
        {
            return client;
        }
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string responseBody;
        private readonly HttpStatusCode statusCode;
        private readonly Exception? exception;

        public CapturingHandler(
            string? responseBody = null,
            HttpStatusCode statusCode = HttpStatusCode.OK,
            Exception? exception = null)
        {
            this.responseBody = responseBody ??
                """
                {
                  "choices": [
                    {
                      "message": {
                        "content": "provider response"
                      }
                    }
                  ],
                  "usage": {
                    "prompt_tokens": 3,
                    "completion_tokens": 2
                  }
                }
                """;
            this.statusCode = statusCode;
            this.exception = exception;
        }

        public string CapturedBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (exception is not null)
            {
                throw exception;
            }

            CapturedBody = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T value)
        {
            CurrentValue = value;
        }

        public T CurrentValue { get; }

        public T Get(string? name)
        {
            return CurrentValue;
        }

        public IDisposable? OnChange(Action<T, string?> listener)
        {
            return null;
        }
    }
}
