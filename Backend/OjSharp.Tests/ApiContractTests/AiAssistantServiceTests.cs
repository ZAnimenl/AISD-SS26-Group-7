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

public sealed class AiAssistantServiceTests
{
    [Fact]
    public async Task Generate_response_includes_workspace_and_public_run_context()
    {
        var handler = new CapturingHandler(
            """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"response_markdown\":\"Check the pending count first.\",\"semantic_tags\":[\"debugging\"],\"suggestion\":{\"target_file\":\"todo_summary_panel.py\",\"language\":\"python\",\"replacement_code\":\"def build_summary(todos):\\n    return {'total': len(todos), 'completed': 0, 'pending': len(todos), 'message': ''}\\n\\ndef render_summary_panel(todos):\\n    return '<section></section>'\\n\",\"apply_label\":\"Apply to todo_summary_panel.py\"}}"
                  }
                }
              ],
              "usage": {
                "prompt_tokens": 25,
                "completion_tokens": 12
              }
            }
            """);
        var service = CreateAssistantService(handler);

        var result = await service.GenerateResponseAsync(
            AiInteractionTypes.Debugging,
            "Why did my run fail?",
            "python",
            "todo_summary_panel.py",
            "def build_summary(todos):\n    return {}\n",
            new Dictionary<string, string>
            {
                ["todo_summary_panel.py"] = "def build_summary(todos):\n    return {}\n",
                ["helper.py"] = "def is_done(todo):\n    return todo.get('completed') is True\n"
            },
            new Dictionary<string, string>
            {
                ["todo_summary_panel.py"] = "def build_summary(todos):\n    return {}\n\ndef render_summary_panel(todos):\n    return '<section></section>'\n",
                ["helper.py"] = "def is_done(todo):\n    return todo.get('completed') is True\n"
            },
            new AiRunContextRequest(
                "runtime_error",
                Stderr: "NameError: name 'message' is not defined",
                TestResults:
                [
                    new AiRunTestResultRequest("Browser preview render", false, "NameError")
                ]),
            "Add a Todo Summary Panel",
            "Count completed and pending todos.",
            ["todo_summary_panel.py", "helper.py"],
            CancellationToken.None);

        using var request = JsonDocument.Parse(handler.CapturedBody);
        var root = request.RootElement;
        Assert.Equal("json_object", root.GetProperty("response_format").GetProperty("type").GetString());
        var systemPrompt = root.GetProperty("messages")[0].GetProperty("content").GetString();
        var userPrompt = root.GetProperty("messages")[1].GetProperty("content").GetString();

        Assert.Contains("Active file: todo_summary_panel.py", systemPrompt);
        Assert.Contains("File: helper.py", userPrompt);
        Assert.Contains("Starter file: todo_summary_panel.py", userPrompt);
        Assert.Contains("def render_summary_panel", userPrompt);
        Assert.Contains("Latest public run feedback", userPrompt);
        Assert.Contains("NameError: name 'message' is not defined", userPrompt);
        Assert.Contains("Browser preview render", userPrompt);
        Assert.Equal("Check the pending count first.", result.ResponseMarkdown);
        Assert.NotNull(result.Suggestion);
        Assert.Equal("todo_summary_panel.py", result.Suggestion.TargetFile);
    }

    [Fact]
    public async Task Generate_response_rejects_suggestion_for_non_active_file()
    {
        var handler = new CapturingHandler(
            """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"response_markdown\":\"Use the active file.\",\"semantic_tags\":[\"code_suggestion\"],\"suggestion\":{\"target_file\":\"other.py\",\"language\":\"python\",\"replacement_code\":\"print('wrong target')\",\"apply_label\":\"Apply\"}}"
                  }
                }
              ],
              "usage": {
                "prompt_tokens": 20,
                "completion_tokens": 10
              }
            }
            """);
        var service = CreateAssistantService(handler);

        var result = await service.GenerateResponseAsync(
            AiInteractionTypes.CodeSuggestion,
            "Suggest an edit",
            "python",
            "todo_summary_panel.py",
            "def build_summary(todos):\n    return {}\n",
            new Dictionary<string, string> { ["todo_summary_panel.py"] = "def build_summary(todos):\n    return {}\n" },
            new Dictionary<string, string> { ["todo_summary_panel.py"] = "def build_summary(todos):\n    return {}\n" },
            null,
            "Add a Todo Summary Panel",
            "Count completed and pending todos.",
            ["todo_summary_panel.py"],
            CancellationToken.None);

        Assert.Null(result.Suggestion);
        Assert.Equal("Use the active file.", result.ResponseMarkdown);
    }

    [Fact]
    public async Task Generate_response_rejects_suggestion_that_drops_required_starter_function()
    {
        var handler = new CapturingHandler(
            """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"response_markdown\":\"Preserve starter names.\",\"semantic_tags\":[\"code_suggestion\"],\"suggestion\":{\"target_file\":\"todo_summary_panel.py\",\"language\":\"python\",\"replacement_code\":\"def todo_summary_panel(todos):\\n    return ''\\n\",\"apply_label\":\"Apply\"}}"
                  }
                }
              ],
              "usage": {
                "prompt_tokens": 20,
                "completion_tokens": 10
              }
            }
            """);
        var service = CreateAssistantService(handler);

        var result = await service.GenerateResponseAsync(
            AiInteractionTypes.CodeSuggestion,
            "Suggest an edit",
            "python",
            "todo_summary_panel.py",
            "completed = 0\npending = 0\n",
            new Dictionary<string, string> { ["todo_summary_panel.py"] = "completed = 0\npending = 0\n" },
            new Dictionary<string, string>
            {
                ["todo_summary_panel.py"] = "def build_summary(todos):\n    return {}\n\ndef render_summary_panel(todos):\n    return '<section></section>'\n"
            },
            null,
            "Add a Todo Summary Panel",
            "Count completed and pending todos.",
            ["todo_summary_panel.py"],
            CancellationToken.None);

        Assert.Null(result.Suggestion);
        Assert.Equal("Preserve starter names.", result.ResponseMarkdown);
    }

    private static AiAssistantService CreateAssistantService(CapturingHandler handler)
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

        return new AiAssistantService(completionService);
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
