using System.Net;
using System.Text;
using System.Text.Json;
using Backend.Configuration;
using Backend.Contracts;
using Backend.Domain;
using Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace OjSharp.Tests.ApiContractTests;

public sealed class AssessmentDraftGenerationServiceTests
{
    [Fact]
    public async Task Generate_question_draft_reports_provider_truncation_before_json_parse_error()
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
                "prompt_tokens": 300,
                "completion_tokens": 8192
              }
            }
            """);
        var service = CreateDraftService(handler);

        var exception = await Assert.ThrowsAsync<AiDraftGenerationException>(() =>
            service.GenerateQuestionDraftAsync(
                Guid.NewGuid(),
                new GenerateQuestionDraftRequest(
                    TaskTypes.RestApiDevelopment,
                    "medium",
                    ["python", "javascript"]),
                sharedPrototypeReference: null,
                sortOrder: 1,
                CancellationToken.None));

        using var request = JsonDocument.Parse(handler.CapturedBody);
        Assert.Equal(8192, request.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Contains("cut off by the provider output limit", exception.Message);
        Assert.DoesNotContain("not valid JSON", exception.Message);
    }

    private static AssessmentDraftGenerationService CreateDraftService(CapturingHandler handler)
    {
        var completionService = new AiCompletionService(
            new SingleClientFactory(new HttpClient(handler)),
            new StaticOptionsMonitor<DeepseekOptions>(new DeepseekOptions { Enabled = false }),
            new StaticOptionsMonitor<LocalLlmOptions>(new LocalLlmOptions
            {
                Enabled = true,
                BaseUrl = "http://local-llm.test",
                Model = "test-model"
            }),
            NullLogger<AiCompletionService>.Instance);

        return new AssessmentDraftGenerationService(completionService);
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

        public CapturingHandler(string responseBody)
        {
            this.responseBody = responseBody;
        }

        public string CapturedBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CapturedBody = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
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
