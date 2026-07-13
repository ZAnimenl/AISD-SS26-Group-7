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

public sealed class JavaScriptParallelDraftGenerationTests
{
    [Theory]
    [InlineData(TaskTypes.RestApiDevelopment)]
    [InlineData(TaskTypes.BugFix)]
    public async Task JavaScript_only_backend_drafts_use_canonical_modules_and_javascript_tests(string taskType)
    {
        var handler = new PromptAwareHandler(OpenAiResponse(JavaScriptTaskContent(taskType)), BaselineOpenAiResponse());
        var service = CreateDraftService(handler);

        var question = await service.GenerateQuestionDraftAsync(
            Guid.NewGuid(),
            new GenerateQuestionDraftRequest(taskType, "hard", ["javascript"]),
            sharedPrototypeReference: null,
            sortOrder: 1,
            CancellationToken.None);

        Assert.Equal(["javascript"], JsonDocumentSerializer.Deserialize(question.LanguageConstraintsJson, Array.Empty<string>()));
        var starterCode = JsonDocumentSerializer.DeserializeStarterCode(question.StarterCodeJson);
        Assert.Contains("server.js", starterCode["javascript"].Keys);
        Assert.Contains("controllers.js", starterCode["javascript"].Keys);
        Assert.Contains("services.js", starterCode["javascript"].Keys);
        Assert.All(question.TestCases, testCase =>
        {
            var testCode = JsonDocumentSerializer.Deserialize(testCase.TestCodeJson, new Dictionary<string, string>());
            Assert.True(testCode.TryGetValue("javascript", out var javascriptTest));
            Assert.False(string.IsNullOrWhiteSpace(javascriptTest));
            Assert.DoesNotContain("python", testCode.Keys);
        });
        Assert.Contains("Supported languages: javascript", handler.FirstUserPrompt);
    }

    [Fact]
    public async Task Provider_cannot_replace_the_requested_language_contract()
    {
        var javascriptDraft = JavaScriptTaskContent(TaskTypes.RestApiDevelopment);
        var pythonOnlyDraft = javascriptDraft.Replace(
            "\"language_constraints\":[\"javascript\"]",
            "\"language_constraints\":[\"python\"]",
            StringComparison.Ordinal);
        var handler = new OrderedHandler(
            OpenAiResponse(pythonOnlyDraft),
            OpenAiResponse(javascriptDraft),
            BaselineOpenAiResponse(),
            BaselineOpenAiResponse());
        var service = CreateDraftService(handler);

        var question = await service.GenerateQuestionDraftAsync(
            Guid.NewGuid(),
            new GenerateQuestionDraftRequest(TaskTypes.RestApiDevelopment, "hard", ["javascript"]),
            sharedPrototypeReference: null,
            sortOrder: 1,
            CancellationToken.None);

        Assert.Equal(["javascript"], JsonDocumentSerializer.Deserialize(question.LanguageConstraintsJson, Array.Empty<string>()));
        Assert.Equal(4, handler.CapturedBodies.Count);
        Assert.Contains("must use exactly the requested languages: javascript", handler.CapturedBodies[1]);
    }

    [Fact]
    public void Canonical_source_supplies_the_javascript_backend_contract()
    {
        var starterCode = new CanonicalPrototypeSource().ApplyCanonicalFiles(
            new Dictionary<string, Dictionary<string, string>>(),
            ["javascript"]);

        Assert.Equal(7, starterCode["javascript"].Count);
        Assert.Contains("server.js", starterCode["javascript"].Keys);
        Assert.Contains("controllers.js", starterCode["javascript"].Keys);
        Assert.Contains("services.js", starterCode["javascript"].Keys);
        Assert.Contains("repositories.js", starterCode["javascript"].Keys);
        Assert.Contains("models.js", starterCode["javascript"].Keys);
        Assert.Contains("schemas.js", starterCode["javascript"].Keys);
        Assert.Contains("environment.js", starterCode["javascript"].Keys);
        Assert.Contains("express", starterCode["javascript"]["server.js"]);
        Assert.Contains("parseTodoInput", starterCode["javascript"]["schemas.js"]);
        Assert.Contains("TODO_DATABASE_PATH", starterCode["javascript"]["environment.js"]);
    }

    [Theory]
    [InlineData(TaskTypes.RestApiDevelopment)]
    [InlineData(TaskTypes.BugFix)]
    public async Task Backend_blueprints_default_to_python_and_javascript(string taskType)
    {
        var handler = new PromptAwareHandler(OpenAiResponse(DualLanguageTaskContent(taskType)), BaselineOpenAiResponse());
        var service = CreateDraftService(handler);

        var questions = await service.GenerateAssessmentDraftAsync(
            Guid.NewGuid(),
            new AssessmentRequest(
                "Dual language Todo assessment",
                "Generate a production-style Todo backend task.",
                50,
                AssessmentStatuses.Draft,
                true,
                TaskTypeCounts: new Dictionary<string, int>
                {
                    [TaskTypes.FrontendUiExtension] = 0,
                    [TaskTypes.RestApiDevelopment] = taskType == TaskTypes.RestApiDevelopment ? 1 : 0,
                    [TaskTypes.DatabaseQuerySchema] = 0,
                    [TaskTypes.BugFix] = taskType == TaskTypes.BugFix ? 1 : 0
                },
                Difficulty: "hard"),
            CancellationToken.None);

        var question = Assert.Single(questions);
        Assert.Equal(
            ["python", "javascript"],
            JsonDocumentSerializer.Deserialize(question.LanguageConstraintsJson, Array.Empty<string>()));
        var starterCode = JsonDocumentSerializer.DeserializeStarterCode(question.StarterCodeJson);
        Assert.Equal(7, starterCode["python"].Count);
        Assert.Equal(7, starterCode["javascript"].Count);
        Assert.All(question.TestCases, testCase =>
        {
            var testCode = JsonDocumentSerializer.Deserialize(testCase.TestCodeJson, new Dictionary<string, string>());
            Assert.False(string.IsNullOrWhiteSpace(testCode["python"]));
            Assert.False(string.IsNullOrWhiteSpace(testCode["javascript"]));
        });
        Assert.Contains("Required languages: python, javascript", handler.FirstUserPrompt);
    }

    [Fact]
    public async Task Blueprint_generation_is_bounded_and_result_order_is_deterministic()
    {
        var handler = new PromptAwareHandler(OpenAiResponse(SqlTaskContent()), BaselineOpenAiResponse(), delayMilliseconds: 60);
        var service = CreateDraftService(handler);

        var questions = await service.GenerateAssessmentDraftAsync(
            Guid.NewGuid(),
            new AssessmentRequest(
                "Parallel Todo database tasks",
                "Generate several transaction-safe Todo database tasks.",
                50,
                AssessmentStatuses.Draft,
                true,
                TaskTypeCounts: new Dictionary<string, int>
                {
                    [TaskTypes.FrontendUiExtension] = 0,
                    [TaskTypes.RestApiDevelopment] = 0,
                    [TaskTypes.DatabaseQuerySchema] = 3,
                    [TaskTypes.BugFix] = 0
                },
                Difficulty: "hard"),
            CancellationToken.None);

        Assert.Equal([1, 2, 3], questions.Select(question => question.SortOrder));
        Assert.Equal([34, 33, 33], questions.Select(question => question.MaxScore));
        Assert.All(questions, question => Assert.Equal(TaskTypes.DatabaseQuerySchema, question.TaskType));
        Assert.Equal(2, handler.MaximumConcurrency);
        Assert.Equal(3, handler.GenerationCallCount);
    }

    private static AssessmentDraftGenerationService CreateDraftService(HttpMessageHandler handler)
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
        return new AssessmentDraftGenerationService(
            completionService,
            new CanonicalPrototypeSource(),
            new TokenEfficiencyReferenceBaselineService(completionService));
    }

    private static string JavaScriptTaskContent(string taskType)
    {
        var isBugFix = taskType == TaskTypes.BugFix;
        var description = isBugFix
            ? "Context: The default Todo List Node service has a race condition between deferred persistence and undo delete behavior across repository, service, and controller modules. Diagnose stale cached state, restore deleted Todo records atomically, and preserve backward compatibility for REST clients. Require deterministic error recovery, validation, cache invalidation, regression protection, and concurrency handling. Edge cases include repeated undo calls, missing identifiers, partial writes, duplicate requests, and restart after failure. Acceptance criteria require public and hidden Jest tests to pass while canonical routes and response fields remain unchanged."
            : "Context: The default Todo List Node API needs idempotent batch updates and cursor pagination across controller, service, and repository modules. Extend canonical Express routes while preserving Todo response fields and file persistence. Require request validation, authorization hooks, optimistic concurrency, deterministic conflict responses, audit-friendly retry behavior, and rollback. Edge cases include duplicate request identifiers, stale cursors, missing records, partial failures, concurrent updates, and empty pages. Acceptance criteria require every public and hidden Jest/Supertest check to pass without changing the original Todo CRUD and toggle contracts.";
        var testCode = "const { createApp } = require('./server.js'); test('Todo contract', () => expect(typeof createApp).toBe('function'));";

        return JsonSerializer.Serialize(new
        {
            tasks = new[]
            {
                new
                {
                    title = isBugFix ? "Todo Race Condition Cache Regression Recovery" : "Idempotent Paginated Todo JavaScript API",
                    task_type = taskType,
                    difficulty = "hard",
                    verification_mode = isBugFix ? VerificationModes.RegressionTest : VerificationModes.ApiResponseCheck,
                    problem_description_markdown = description,
                    language_constraints = new[] { "javascript" },
                    starter_code = new Dictionary<string, Dictionary<string, string>>
                    {
                        ["javascript"] = new() { ["task-extension.js"] = "// Extend the canonical Todo modules." }
                    },
                    test_cases = TestCases("javascript", testCode)
                }
            }
        });
    }

    private static string DualLanguageTaskContent(string taskType)
    {
        var isBugFix = taskType == TaskTypes.BugFix;
        var description = isBugFix
            ? "Context: The canonical Todo backends contain interacting defects in deferred persistence, cache invalidation, and conflict recovery across repository, service, controller, schema, and environment modules. Diagnose stale state after failed writes while preserving every REST route and response field in both implementations. Require validation, idempotency, concurrency handling, rollback, auditability, and deterministic error recovery. Edge cases include duplicate requests, missing identifiers, partial writes, repeated undo attempts, restarts, and simultaneous updates. Acceptance criteria require equivalent Python pytest and JavaScript Jest regression behavior, unchanged public interfaces, isolated test storage, and no student-installed dependencies."
            : "Context: Extend both canonical Todo APIs with idempotent batch updates and cursor pagination across repository, service, controller, schema, and environment modules. Preserve every existing route and response field while adding validation, authorization hooks, concurrency control, rollback, caching, and auditability. Edge cases include duplicate request identifiers, stale cursors, missing records, partial failures, simultaneous updates, empty pages, and process restart. Acceptance criteria require equivalent FastAPI and Express behavior, deterministic conflict responses, isolated file or SQLite persistence, passing Python pytest and JavaScript Jest checks, backward-compatible CRUD and toggle operations, and no student-installed dependencies.";
        var pythonTest = "from main import app\n\ndef test_todo_contract():\n    assert app is not None\n";
        var javascriptTest = "const { createApp } = require('./server.js'); test('Todo contract', () => expect(typeof createApp).toBe('function'));";

        return JsonSerializer.Serialize(new
        {
            tasks = new[]
            {
                new
                {
                    title = isBugFix ? "Cross-Runtime Todo Persistence Regression" : "Cross-Runtime Idempotent Todo Pagination",
                    task_type = taskType,
                    difficulty = "hard",
                    verification_mode = isBugFix ? VerificationModes.RegressionTest : VerificationModes.ApiResponseCheck,
                    problem_description_markdown = description,
                    language_constraints = new[] { "python", "javascript" },
                    starter_code = new Dictionary<string, Dictionary<string, string>>
                    {
                        ["python"] = new() { ["main.py"] = "# Extend the canonical Todo modules." },
                        ["javascript"] = new() { ["server.js"] = "// Extend the canonical Todo modules." }
                    },
                    test_cases = new object[]
                    {
                        DualLanguageTestCase("Public contract", TestCaseVisibilities.Public, pythonTest, javascriptTest),
                        DualLanguageTestCase("Public behavior", TestCaseVisibilities.Public, pythonTest, javascriptTest),
                        DualLanguageTestCase("Hidden edge cases", TestCaseVisibilities.Hidden, pythonTest, javascriptTest),
                        DualLanguageTestCase("Hidden regression", TestCaseVisibilities.Hidden, pythonTest, javascriptTest)
                    }
                }
            }
        });
    }

    private static object DualLanguageTestCase(
        string name,
        string visibility,
        string pythonTest,
        string javascriptTest) => new
        {
            name,
            visibility,
            test_code = new Dictionary<string, string>
            {
                ["python"] = pythonTest,
                ["javascript"] = javascriptTest
            }
        };

    private static string SqlTaskContent()
    {
        const string description = "Context: The default Todo List database needs transaction-safe task history and reconciliation across schema, seed, and reporting logic. Preserve Todo, assignee, dependency, and audit contracts. Require transactions, window functions, constraints, migrations, rollback, idempotency, concurrency, audit aggregation, and referential integrity. Handle duplicate updates, null due dates, cycles, concurrent completion changes, orphaned audit rows, and timestamp ties deterministically. Acceptance criteria require public and hidden checks to pass, invalid writes to fail, aggregates to remain correct after retries, and existing prototype consumers to query the original views unchanged.";
        const string testCode = "const fs = require('fs'); test('SQL contract', () => expect(fs.readFileSync('solution.sql', 'utf8')).toContain('Todo'));";
        return JsonSerializer.Serialize(new
        {
            tasks = new[]
            {
                new
                {
                    title = "Transaction-Safe Todo Audit Reconciliation",
                    task_type = TaskTypes.DatabaseQuerySchema,
                    difficulty = "hard",
                    verification_mode = VerificationModes.DatabaseResultCheck,
                    problem_description_markdown = description,
                    language_constraints = new[] { "sql" },
                    starter_code = new Dictionary<string, Dictionary<string, string>>
                    {
                        ["sql"] = new() { ["solution.sql"] = "-- Implement transaction-safe Todo reporting." }
                    },
                    test_cases = TestCases("sql", testCode)
                }
            }
        });
    }

    private static object[] TestCases(string language, string testCode) =>
    [
        new { name = "Public contract", visibility = TestCaseVisibilities.Public, test_code = new Dictionary<string, string> { [language] = testCode } },
        new { name = "Public behavior", visibility = TestCaseVisibilities.Public, test_code = new Dictionary<string, string> { [language] = testCode } },
        new { name = "Hidden edge cases", visibility = TestCaseVisibilities.Hidden, test_code = new Dictionary<string, string> { [language] = testCode } },
        new { name = "Hidden regression", visibility = TestCaseVisibilities.Hidden, test_code = new Dictionary<string, string> { [language] = testCode } }
    ];

    private static string OpenAiResponse(string content) => JsonSerializer.Serialize(new
    {
        choices = new[] { new { message = new { content }, finish_reason = "stop" } },
        usage = new { prompt_tokens = 300, completion_tokens = 1200 }
    });

    private static string BaselineOpenAiResponse() => OpenAiResponse(JsonSerializer.Serialize(new
    {
        goal = "Implement Todo behavior.",
        code_context = "Todo starter modules.",
        observed_behavior = "Implementation incomplete.",
        constraint = "Preserve Todo contracts.",
        standard_steps = new[] { new { purpose = "Implement", minimal_input = "Fix Todo", public_verification = "Run public tests" } }
    }));

    private sealed class PromptAwareHandler : HttpMessageHandler
    {
        private readonly string generationResponse;
        private readonly string baselineResponse;
        private readonly int delayMilliseconds;
        private int currentConcurrency;
        private int maximumConcurrency;
        private int generationCallCount;

        public PromptAwareHandler(string generationResponse, string baselineResponse, int delayMilliseconds = 0)
        {
            this.generationResponse = generationResponse;
            this.baselineResponse = baselineResponse;
            this.delayMilliseconds = delayMilliseconds;
        }

        public int MaximumConcurrency => Volatile.Read(ref maximumConcurrency);
        public int GenerationCallCount => Volatile.Read(ref generationCallCount);
        public string FirstUserPrompt { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var concurrency = Interlocked.Increment(ref currentConcurrency);
            UpdateMaximum(concurrency);
            try
            {
                var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
                using var document = JsonDocument.Parse(body);
                var messages = document.RootElement.GetProperty("messages");
                var systemPrompt = messages[0].GetProperty("content").GetString() ?? "";
                var isGeneration = systemPrompt.Contains("coding assessment draft tasks", StringComparison.Ordinal);
                if (isGeneration)
                {
                    Interlocked.Increment(ref generationCallCount);
                    FirstUserPrompt = messages[1].GetProperty("content").GetString() ?? "";
                }
                if (delayMilliseconds > 0) await Task.Delay(delayMilliseconds, cancellationToken);
                return Response(isGeneration ? generationResponse : baselineResponse);
            }
            finally
            {
                Interlocked.Decrement(ref currentConcurrency);
            }
        }

        private void UpdateMaximum(int candidate)
        {
            var current = Volatile.Read(ref maximumConcurrency);
            while (candidate > current)
            {
                var observed = Interlocked.CompareExchange(ref maximumConcurrency, candidate, current);
                if (observed == current) return;
                current = observed;
            }
        }
    }

    private sealed class OrderedHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> responses = new(responses);
        public List<string> CapturedBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedBodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
            return Response(responses.Dequeue());
        }
    }

    private static HttpResponseMessage Response(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, "application/json")
    };

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
