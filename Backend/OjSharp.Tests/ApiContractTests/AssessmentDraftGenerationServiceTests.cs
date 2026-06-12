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

    [Fact]
    public async Task Generate_question_draft_rejects_sql_task_without_sql_test_code()
    {
        var handler = new CapturingHandler(OpenAiResponse(
            """
            {
              "tasks": [
                {
                  "title": "Write SQL queries for employees",
                  "task_type": "database_query_schema",
                  "difficulty": "medium",
                  "verification_mode": "database_result_check",
                  "starter_prototype_reference": null,
                  "problem_description_markdown": "Write SQL queries in solution.sql.",
                  "language_constraints": ["sql"],
                  "starter_code": {
                    "sql": {
                      "solution.sql": "-- Write your queries here\n"
                    }
                  },
                  "starter_files_metadata": {
                    "sql": {
                      "solution.sql": "editable"
                    }
                  },
                  "verification_metadata": {
                    "primary_view": "database_result_check"
                  },
                  "grading_configuration": {
                    "runner": "automated_tests",
                    "requires_student_install": "false"
                  },
                  "traceability_metadata": {
                    "requirements": "REQ-18f"
                  },
                  "max_score": 25,
                  "test_cases": [
                    {
                      "name": "Query 1 returns employees",
                      "visibility": "public",
                      "test_code": {
                        "javascript": "test('placeholder', () => expect(true).toBe(true));"
                      },
                      "traceability_metadata": {
                        "requirements": "REQ-52"
                      }
                    },
                    {
                      "name": "Hidden query validation",
                      "visibility": "hidden",
                      "test_code": {
                        "sql": "const fs = require('fs'); test('solution exists', () => expect(fs.readFileSync('solution.sql', 'utf8')).toContain('SELECT'));"
                      },
                      "traceability_metadata": {
                        "requirements": "REQ-53"
                      }
                    }
                  ]
                }
              ]
            }
            """));
        var service = CreateDraftService(handler);

        var exception = await Assert.ThrowsAsync<AiDraftGenerationException>(() =>
            service.GenerateQuestionDraftAsync(
                Guid.NewGuid(),
                new GenerateQuestionDraftRequest(
                    TaskTypes.DatabaseQuerySchema,
                    "medium",
                    ["sql"]),
                sharedPrototypeReference: null,
                sortOrder: 1,
                CancellationToken.None));

        Assert.Contains("missing test code for language 'sql'", exception.Message);
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

    private static string OpenAiResponse(string content)
    {
        return JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    finish_reason = "stop",
                    message = new
                    {
                        content
                    }
                }
            },
            usage = new
            {
                prompt_tokens = 300,
                completion_tokens = 1200
            }
        });
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
