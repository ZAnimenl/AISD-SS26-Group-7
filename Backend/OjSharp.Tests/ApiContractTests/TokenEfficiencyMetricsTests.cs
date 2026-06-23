using System.Net;
using System.Text;
using System.Text.Json;
using Backend.Configuration;
using Backend.Domain;
using Backend.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace OjSharp.Tests.ApiContractTests;

public sealed class TokenEfficiencyMetricsTests
{
    [Fact]
    public void Density_uses_unicode_scalar_characters_and_provider_tokens()
    {
        var metrics = TokenEfficiencyMetrics.Calculate(
        [
            new AiInteraction
            {
                Message = "Goal must pass test",
                ActiveFileContent = "😀",
                ResponseMarkdown = "ok",
                InputTokens = 2,
                OutputTokens = 1
            }
        ]);

        Assert.Equal(20, metrics.PromptSource.Characters);
        Assert.Equal(2, metrics.PromptSource.Tokens);
        Assert.Equal(10, metrics.PromptSource.CharactersPerToken);
        Assert.Equal(0.1, metrics.PromptSource.TokensPerCharacter);
        Assert.Equal(2, metrics.Response.Characters);
        Assert.Equal(3, metrics.ContextSignalsProvided);
        Assert.Equal(4, metrics.RequiredContextSignals);
    }

    [Fact]
    public async Task Reference_baseline_uses_provider_measured_full_and_compact_input_tokens()
    {
        var completionService = CreateCompletionService(new SequenceHandler(
            Completion(ReferenceResponse(), 100, 10),
            Completion(ReferenceResponse(), 40, 5)));
        var service = new TokenEfficiencyReferenceBaselineService(completionService);
        var question = new Question
        {
            Id = Guid.NewGuid(),
            Title = "Preserve todo integrity",
            ProblemDescriptionMarkdown = "Validate updates and preserve compatibility.",
            StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
            {
                ["python"] = new() { ["services.py"] = "def update(): pass" }
            })
        };

        var baseline = await service.RunAsync(question, CancellationToken.None);

        Assert.Equal("complete", baseline.Status);
        Assert.Equal(100, baseline.FullInputTokens);
        Assert.Equal(40, baseline.CompactInputTokens);
        Assert.Equal(0.4, baseline.CompressionRate);
        Assert.Equal(2.5, baseline.CompressionRatio);
        Assert.Equal(1, baseline.StructuralUtilityRetention);
        Assert.Equal(60, baseline.ReferenceScore);
        Assert.Equal(2, baseline.StandardSteps!.Count);
        Assert.NotNull(baseline.CompactPromptDensity);
        Assert.NotNull(baseline.CompactResponseDensity);

        TaskAiUsageBenchmarkFactory.AttachReferenceBaseline(question, baseline);
        var benchmark = TaskAiUsageBenchmarkFactory.Read(question.GradingConfigurationJson, question.TaskType, question.Difficulty);
        Assert.Equal(45, benchmark.ReferenceTotalTokens);
        Assert.NotNull(benchmark.ReferenceBaseline);
        Assert.Equal(baseline.ReferenceScore, benchmark.ReferenceBaseline!.ReferenceScore);
        Assert.Equal(baseline.CompactPromptDensity, benchmark.ReferenceBaseline.CompactPromptDensity);
        Assert.Equal(2, benchmark.ReferenceBaseline.StandardSteps!.Count);
    }

    [Fact]
    public async Task Reference_baseline_is_unavailable_without_minimal_input_standard_steps()
    {
        var completionService = CreateCompletionService(new SequenceHandler(
            Completion("{\"goal\":\"g\",\"code_context\":\"c\",\"observed_behavior\":\"o\",\"constraint\":\"k\"}", 100, 10),
            Completion("{\"goal\":\"g\",\"code_context\":\"c\",\"observed_behavior\":\"o\",\"constraint\":\"k\"}", 40, 5)));
        var baseline = await new TokenEfficiencyReferenceBaselineService(completionService).RunAsync(new Question
        {
            Title = "A task",
            ProblemDescriptionMarkdown = "A public task description.",
            StarterCodeJson = "{}"
        }, CancellationToken.None);

        Assert.Equal("unavailable", baseline.Status);
        Assert.Equal("baseline_response_missing_required_context", baseline.FailureReason);
    }

    private static AiCompletionService CreateCompletionService(HttpMessageHandler handler) => new(
        new SingleClientFactory(handler),
        new StaticOptionsMonitor<DeepseekOptions>(new DeepseekOptions { Enabled = false }),
        new StaticOptionsMonitor<LocalLlmOptions>(new LocalLlmOptions
        {
            Enabled = true,
            BaseUrl = "http://local-llm.test",
            Model = "test-model"
        }),
        NullLogger<AiCompletionService>.Instance);

    private static string Completion(string content, int inputTokens, int outputTokens) => JsonSerializer.Serialize(new
    {
        choices = new[] { new { finish_reason = "stop", message = new { content } } },
        usage = new { prompt_tokens = inputTokens, completion_tokens = outputTokens }
    });

    private static string ReferenceResponse() => """
    {"goal":"g","code_context":"c","observed_behavior":"o","constraint":"k","standard_steps":[{"purpose":"Inspect the failing public contract","minimal_input":"Explain the failing public request using the active file.","public_verification":"Run the public request check."},{"purpose":"Validate the change","minimal_input":"Review the smallest compatible fix.","public_verification":"Run the public regression check."}]}
    """;

    private sealed class SequenceHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> responses = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = responses.Dequeue();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T> where T : class
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
