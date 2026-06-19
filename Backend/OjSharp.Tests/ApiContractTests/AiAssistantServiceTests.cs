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

    [Fact]
    public async Task Generate_response_accepts_workspace_action_for_visible_non_active_file()
    {
        var handler = new CapturingHandler(
            """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"response_markdown\":\"I can update the button and click handler, then run checks.\",\"semantic_tags\":[\"code_suggestion\"],\"suggestion\":null,\"workspace_actions\":[{\"type\":\"replace_file\",\"target_file\":\"index.html\",\"language\":\"javascript\",\"replacement_code\":\"<button id=\\\"addBtn\\\">Add Task</button><button id=\\\"clearBtn\\\">Clear All</button><ul id=\\\"taskList\\\"></ul><script src=\\\"app.js\\\"></script>\",\"label\":\"Apply to index.html\"},{\"type\":\"replace_file\",\"target_file\":\"app.js\",\"language\":\"javascript\",\"replacement_code\":\"const list = document.getElementById('taskList');\\ndocument.getElementById('clearBtn').addEventListener('click', () => { list.innerHTML = ''; });\\n\",\"label\":\"Apply to app.js\"},{\"type\":\"run_public_checks\",\"label\":\"Run public checks\"}]}"
                  }
                }
              ],
              "usage": {
                "prompt_tokens": 24,
                "completion_tokens": 16
              }
            }
            """);
        var service = CreateAssistantService(handler);

        var result = await service.GenerateResponseAsync(
            AiInteractionTypes.CodeSuggestion,
            "Update app.js and run tests.",
            "javascript",
            "index.html",
            "<button id=\"clearBtn\">Clear All</button><script src=\"app.js\"></script>",
            new Dictionary<string, string>
            {
                ["index.html"] = "<button id=\"clearBtn\">Clear All</button><script src=\"app.js\"></script>",
                ["app.js"] = "const list = document.getElementById('taskList');\n"
            },
            new Dictionary<string, string>
            {
                ["index.html"] = "<button id=\"clearBtn\">Clear All</button><script src=\"app.js\"></script>",
                ["app.js"] = "const list = document.getElementById('taskList');\n"
            },
            null,
            "Add a Clear All Button",
            "Add a button that clears all tasks.",
            ["index.html", "app.js"],
            CancellationToken.None);

        Assert.Equal(3, result.WorkspaceActions.Count);
        Assert.Equal(AiWorkspaceActionTypes.ReplaceFile, result.WorkspaceActions[0].Type);
        Assert.Equal("index.html", result.WorkspaceActions[0].TargetFile);
        Assert.Equal(AiWorkspaceActionTypes.ReplaceFile, result.WorkspaceActions[1].Type);
        Assert.Equal("app.js", result.WorkspaceActions[1].TargetFile);
        Assert.Equal(AiWorkspaceActionTypes.RunPublicChecks, result.WorkspaceActions[2].Type);
    }

    [Fact]
    public async Task Generate_response_accepts_html_action_language_for_javascript_workspace_file()
    {
        var handler = new CapturingHandler(CompletionJson(
            """
            {"response_markdown":"Update the HTML button.","semantic_tags":["code_suggestion"],"workspace_actions":[{"type":"replace_file","target_file":"index.html","language":"html","replacement_code":"<button id=\"addBtn\">Add Task</button><button id=\"clearBtn\">Clear All</button><ul id=\"taskList\"></ul><script src=\"app.js\"></script>","label":"Apply to index.html"}]}
            """,
            24,
            16));
        var service = CreateAssistantService(handler);

        var result = await service.GenerateResponseAsync(
            AiInteractionTypes.CodeSuggestion,
            "Update index.html for the Clear All task.",
            "javascript",
            "index.html",
            "<button id=\"addBtn\">Add Task</button><ul id=\"taskList\"></ul><script src=\"app.js\"></script>",
            new Dictionary<string, string>
            {
                ["index.html"] = "<button id=\"addBtn\">Add Task</button><ul id=\"taskList\"></ul><script src=\"app.js\"></script>",
                ["app.js"] = "const taskList = document.getElementById('taskList');\n"
            },
            new Dictionary<string, string>
            {
                ["index.html"] = "<button id=\"addBtn\">Add Task</button><ul id=\"taskList\"></ul><script src=\"app.js\"></script>",
                ["app.js"] = "const taskList = document.getElementById('taskList');\n"
            },
            null,
            "Add a Clear All Button",
            "Add a button that clears all tasks.",
            ["index.html", "app.js"],
            CancellationToken.None);

        var action = Assert.Single(result.WorkspaceActions);
        Assert.Equal("index.html", action.TargetFile);
        Assert.Equal("javascript", action.Language);
    }

    [Fact]
    public async Task Generate_response_parses_json_from_markdown_fence_without_showing_the_envelope()
    {
        var handler = new CapturingHandler(CompletionJson(
            """
            ```json
            {"response_markdown":"Review this edit first.","semantic_tags":["code_suggestion"],"workspace_actions":[{"type":"replace_file","target_file":"app.js","language":"javascript","replacement_code":"const list = document.getElementById('taskList');\n","label":"Apply"}]}
            ```
            """,
            24,
            16));
        var service = CreateAssistantService(handler);

        var result = await service.GenerateResponseAsync(
            AiInteractionTypes.CodeSuggestion,
            "Update app.js.",
            "javascript",
            "app.js",
            "const list = document.getElementById('taskList');\n",
            new Dictionary<string, string> { ["app.js"] = "const list = document.getElementById('taskList');\n" },
            new Dictionary<string, string> { ["app.js"] = "const list = document.getElementById('taskList');\n" },
            null,
            "Update Todo behavior",
            "Update the visible Todo behavior.",
            ["app.js"],
            CancellationToken.None);

        Assert.Equal("Review this edit first.", result.ResponseMarkdown);
        Assert.Single(result.WorkspaceActions);
        Assert.DoesNotContain("response_markdown", result.ResponseMarkdown);
    }

    [Fact]
    public async Task Generate_response_rejects_workspace_action_for_non_visible_file()
    {
        var handler = new CapturingHandler(
            """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\"response_markdown\":\"That target is not visible.\",\"semantic_tags\":[\"code_suggestion\"],\"workspace_actions\":[{\"type\":\"replace_file\",\"target_file\":\"hidden.test.js\",\"language\":\"javascript\",\"replacement_code\":\"test('leak', () => {});\",\"label\":\"Apply\"}]}"
                  }
                }
              ],
              "usage": {
                "prompt_tokens": 24,
                "completion_tokens": 16
              }
            }
            """);
        var service = CreateAssistantService(handler);

        var result = await service.GenerateResponseAsync(
            AiInteractionTypes.CodeSuggestion,
            "Edit hidden.test.js",
            "javascript",
            "app.js",
            "const list = document.getElementById('taskList');\n",
            new Dictionary<string, string>
            {
                ["app.js"] = "const list = document.getElementById('taskList');\n"
            },
            new Dictionary<string, string>
            {
                ["app.js"] = "const list = document.getElementById('taskList');\n"
            },
            null,
            "Add a Clear All Button",
            "Add a button that clears all tasks.",
            ["app.js"],
            CancellationToken.None);

        Assert.Empty(result.WorkspaceActions);
    }

    [Fact]
    public async Task Generate_response_rejects_workspace_action_for_client_only_file_name()
    {
        var handler = new CapturingHandler(CompletionJson(
            """
            {"response_markdown":"That file is not part of the starter workspace.","semantic_tags":["code_suggestion"],"workspace_actions":[{"type":"replace_file","target_file":"injected.js","language":"javascript","replacement_code":"console.log('new file');","label":"Apply"}]}
            """,
            24,
            16));
        var service = CreateAssistantService(handler);

        var result = await service.GenerateResponseAsync(
            AiInteractionTypes.CodeSuggestion,
            "Edit injected.js",
            "javascript",
            "app.js",
            "const list = document.getElementById('taskList');\n",
            new Dictionary<string, string>
            {
                ["app.js"] = "const list = document.getElementById('taskList');\n",
                ["injected.js"] = "console.log('client supplied');\n"
            },
            new Dictionary<string, string>
            {
                ["app.js"] = "const list = document.getElementById('taskList');\n"
            },
            null,
            "Add a Clear All Button",
            "Add a button that clears all tasks.",
            ["app.js"],
            CancellationToken.None);

        Assert.Empty(result.WorkspaceActions);
    }

    [Fact]
    public async Task Generate_response_repairs_missing_action_for_explicit_visible_file_request()
    {
        var handler = new CapturingHandler(
            CompletionJson(
                """
                {"response_markdown":"Update index.html and app.js.","semantic_tags":["code_suggestion"],"workspace_actions":[{"type":"replace_file","target_file":"app.js","language":"javascript","replacement_code":"const clearBtn = document.getElementById('clearBtn');\nclearBtn.addEventListener('click', () => document.getElementById('taskList').innerHTML = '');\n","label":"Apply to app.js"}]}
                """,
                24,
                16),
            CompletionJson(
                """
                {"response_markdown":"Update both visible files, then run checks.","semantic_tags":["code_suggestion"],"workspace_actions":[{"type":"replace_file","target_file":"index.html","language":"javascript","replacement_code":"<button id=\"addBtn\">Add Task</button><button id=\"clearBtn\">Clear All</button><ul id=\"taskList\"></ul><script src=\"app.js\"></script>","label":"Apply to index.html"},{"type":"replace_file","target_file":"app.js","language":"javascript","replacement_code":"const clearBtn = document.getElementById('clearBtn');\nclearBtn.addEventListener('click', () => document.getElementById('taskList').innerHTML = '');\n","label":"Apply to app.js"},{"type":"run_public_checks","label":"Run public checks"}]}
                """,
                30,
                20));
        var service = CreateAssistantService(handler);

        var result = await service.GenerateResponseAsync(
            AiInteractionTypes.CodeSuggestion,
            "Please update index.html and app.js for the Clear All task.",
            "javascript",
            "index.html",
            "<button id=\"addBtn\">Add Task</button><ul id=\"taskList\"></ul><script src=\"app.js\"></script>",
            new Dictionary<string, string>
            {
                ["index.html"] = "<button id=\"addBtn\">Add Task</button><ul id=\"taskList\"></ul><script src=\"app.js\"></script>",
                ["app.js"] = "const taskList = document.getElementById('taskList');\n"
            },
            new Dictionary<string, string>
            {
                ["index.html"] = "<button id=\"addBtn\">Add Task</button><ul id=\"taskList\"></ul><script src=\"app.js\"></script>",
                ["app.js"] = "const taskList = document.getElementById('taskList');\n"
            },
            null,
            "Add a Clear All Button",
            "Add a button that clears all tasks.",
            ["index.html", "app.js"],
            CancellationToken.None);

        Assert.Equal(2, handler.RequestCount);
        Assert.Contains("Structured action correction required", handler.CapturedBody);
        Assert.Contains("index.html", result.WorkspaceActions.Select(action => action.TargetFile));
        Assert.Contains("app.js", result.WorkspaceActions.Select(action => action.TargetFile));
        Assert.Equal(54, result.InputTokens);
        Assert.Equal(36, result.OutputTokens);
    }

    private static AiAssistantService CreateAssistantService(CapturingHandler handler)
    {
        var completionService = new AiCompletionService(
            new SingleClientFactory(handler),
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

    private static string CompletionJson(string content, int promptTokens, int completionTokens)
    {
        return JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content
                    }
                }
            },
            usage = new
            {
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens
            }
        });
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler handler;

        public SingleClientFactory(HttpMessageHandler handler)
        {
            this.handler = handler;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly Queue<string> responseBodies;

        public CapturingHandler(params string[] responseBodies)
        {
            this.responseBodies = new Queue<string>(responseBodies.Length > 0
                ? responseBodies
                :
                [
                    """
                    {
                      "choices": [
                        {
                          "message": {
                            "content": "{\"response_markdown\":\"provider response\",\"semantic_tags\":[\"code_suggestion\"],\"suggestion\":null}"
                          }
                        }
                      ],
                      "usage": {
                        "prompt_tokens": 3,
                        "completion_tokens": 2
                      }
                    }
                    """
                ]);
        }

        public string CapturedBody { get; private set; } = "";

        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount += 1;
            CapturedBody = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var responseBody = responseBodies.Count > 1
                ? responseBodies.Dequeue()
                : responseBodies.Peek();

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
