using System.Collections.Concurrent;
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
    public void Optimistic_ui_with_conflict_resolution_is_advanced_frontend_work()
    {
        const string taskText = "Implement Optimistic UI with Conflict Resolution for Todo Toggle";

        var concernCount = AssessmentDraftGenerationService.CountAdvancedConcerns(
            taskText,
            TaskTypes.FrontendUiExtension);

        Assert.Equal(2, concernCount);
        Assert.Equal(2, AssessmentDraftGenerationService.GetMinimumAdvancedConcerns(TaskTypes.FrontendUiExtension));
    }

    [Fact]
    public void Assessment_task_counts_expand_to_the_requested_mix()
    {
        var counts = AssessmentDraftGenerationService.NormalizeTaskTypeCounts(new Dictionary<string, int>
        {
            [TaskTypes.FrontendUiExtension] = 2,
            [TaskTypes.RestApiDevelopment] = 1,
            [TaskTypes.DatabaseQuerySchema] = 0,
            [TaskTypes.BugFix] = 3
        });

        var taskTypes = AssessmentDraftGenerationService.BuildRequestedTaskTypes(counts);

        Assert.Equal(6, taskTypes.Length);
        Assert.Equal(2, taskTypes.Count(type => type == TaskTypes.FrontendUiExtension));
        Assert.Equal(1, taskTypes.Count(type => type == TaskTypes.RestApiDevelopment));
        Assert.Equal(0, taskTypes.Count(type => type == TaskTypes.DatabaseQuerySchema));
        Assert.Equal(3, taskTypes.Count(type => type == TaskTypes.BugFix));
    }

    [Fact]
    public void Assessment_task_counts_are_bounded_and_never_empty()
    {
        var bounded = AssessmentDraftGenerationService.NormalizeTaskTypeCounts(new Dictionary<string, int>
        {
            [TaskTypes.FrontendUiExtension] = 99,
            [TaskTypes.RestApiDevelopment] = 99,
            [TaskTypes.DatabaseQuerySchema] = 99,
            [TaskTypes.BugFix] = 99
        });
        var fallback = AssessmentDraftGenerationService.NormalizeTaskTypeCounts(
            RequiredTaskTypeCounts(0));

        Assert.Equal(12, bounded.Values.Sum());
        Assert.All(bounded.Values, count => Assert.InRange(count, 0, 5));
        Assert.Equal(1, fallback.Values.Sum());
        Assert.Equal(1, fallback[TaskTypes.FrontendUiExtension]);
    }

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
        Assert.Equal(16384, request.RootElement.GetProperty("max_tokens").GetInt32());
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

    [Fact]
    public async Task Generate_question_draft_repairs_missing_language_test_code()
    {
        var starterFiles = new Dictionary<string, string>
        {
            ["schema.sql"] = "CREATE TABLE todo_tasks (id INTEGER PRIMARY KEY, completed INTEGER NOT NULL);",
            ["seed.sql"] = "INSERT INTO todo_tasks (id, completed) VALUES (1, 0), (2, 1);",
            ["solution.sql"] = "-- Implement the transaction-safe Todo task reporting queries."
        };
        var missingTestCode = AdvancedSqlTaskContent(starterFiles)
            .Replace("\"sql\":\"const fs", "\"javascript\":\"const fs", StringComparison.Ordinal);
        var handler = new SequencedHandler(
            OpenAiResponse(missingTestCode),
            OpenAiResponse(AdvancedSqlTaskContent(starterFiles)));
        var service = CreateDraftService(handler);

        var question = await service.GenerateQuestionDraftAsync(
            Guid.NewGuid(),
            new GenerateQuestionDraftRequest(
                TaskTypes.DatabaseQuerySchema,
                "hard",
                ["sql"]),
            sharedPrototypeReference: null,
            sortOrder: 1,
            CancellationToken.None);

        Assert.Equal("Transaction-Safe Todo Audit Reconciliation", question.Title);
        Assert.Equal(3, handler.CallCount);
        Assert.Contains("Every public and hidden test_cases item", handler.CapturedBodies[1]);
        Assert.Contains("required language", handler.CapturedBodies[1]);
    }

    [Fact]
    public async Task Generate_question_draft_repairs_missing_typescript_test_code()
    {
        var missingTestCode = AdvancedTypeScriptTaskContent()
            .Replace("\"typescript\":\"const fs", "\"javascript\":\"const fs", StringComparison.Ordinal);
        var handler = new SequencedHandler(
            OpenAiResponse(missingTestCode),
            OpenAiResponse(AdvancedTypeScriptTaskContent()),
            BaselineOpenAiResponse(),
            BaselineOpenAiResponse());
        var service = CreateDraftService(handler);

        var question = await service.GenerateQuestionDraftAsync(
            Guid.NewGuid(),
            new GenerateQuestionDraftRequest(
                TaskTypes.RestApiDevelopment,
                "hard",
                ["typescript"]),
            sharedPrototypeReference: null,
            sortOrder: 1,
            CancellationToken.None);

        Assert.Equal("Idempotent TypeScript Todo Conflict API", question.Title);
        Assert.Equal(4, handler.CallCount);
        var correctionPrompt = ReadLastUserPrompt(handler.CapturedBodies[1]);
        Assert.Contains("missing test code for language 'typescript'", correctionPrompt);
        Assert.Contains("Every public and hidden test_cases item", correctionPrompt);
        Assert.Contains("typescript", correctionPrompt);
        Assert.Contains("non-empty executable entries", correctionPrompt);
    }

    [Fact]
    public async Task Generate_question_draft_rejects_raw_sql_audit_test_without_setup_rows()
    {
        var handler = new CapturingHandler(OpenAiResponse(AdvancedSqlTaskContent(
            starterFiles: new Dictionary<string, string>
            {
                ["schema.sql"] = "CREATE TABLE todos (id INTEGER PRIMARY KEY, completed INTEGER NOT NULL); CREATE TABLE audit_log (todo_id INTEGER NOT NULL, operation TEXT NOT NULL);",
                ["seed.sql"] = "INSERT INTO todos (id, completed) VALUES (1, 0);",
                ["solution.sql"] = "-- Implement audit triggers and reporting query."
            },
            testCases:
            [
                BuildRawSqlTestCase("Public audit statistics", TestCaseVisibilities.Public, "SELECT operation, COUNT(*) FROM audit_log GROUP BY operation;"),
                BuildRawSqlTestCase("Public audit rows", TestCaseVisibilities.Public, "SELECT todo_id FROM audit_log;"),
                BuildRawSqlTestCase("Hidden audit statistics", TestCaseVisibilities.Hidden, "SELECT operation, COUNT(*) FROM audit_log GROUP BY operation;"),
                BuildRawSqlTestCase("Hidden audit rows", TestCaseVisibilities.Hidden, "SELECT todo_id FROM audit_log;")
            ])));
        var service = CreateDraftService(handler);

        var exception = await Assert.ThrowsAsync<AiDraftGenerationException>(() =>
            service.GenerateQuestionDraftAsync(
                Guid.NewGuid(),
                new GenerateQuestionDraftRequest(
                    TaskTypes.DatabaseQuerySchema,
                    "hard",
                    ["sql"]),
                sharedPrototypeReference: null,
                sortOrder: 1,
                CancellationToken.None));

        Assert.Contains("reads audit_log but does not create audit rows", exception.Message);
    }

    [Fact]
    public void Test_code_coverage_accepts_html_key_for_javascript_dom_tests()
    {
        var testCode = new Dictionary<string, string>
        {
            ["html"] = "test('optimistic toggle updates immediately', () => expect(true).toBe(true));"
        };

        Assert.True(AssessmentDraftGenerationService.HasNonEmptyTestCode(testCode, "javascript"));
    }

    [Fact]
    public async Task Generate_question_draft_rejects_wrong_task_type_before_language_coverage()
    {
        var starterFiles = new Dictionary<string, string>
        {
            ["schema.sql"] = "CREATE TABLE todo_tasks (id INTEGER PRIMARY KEY, completed INTEGER NOT NULL);",
            ["seed.sql"] = "INSERT INTO todo_tasks (id, completed) VALUES (1, 0), (2, 1);",
            ["solution.sql"] = "-- Implement the transaction-safe Todo task reporting queries."
        };
        var wrongTaskType = AdvancedSqlTaskContent(starterFiles)
            .Replace("\"task_type\":\"database_query_schema\"", "\"task_type\":\"bug_fix\"", StringComparison.Ordinal);
        var handler = new CapturingHandler(OpenAiResponse(wrongTaskType));
        var service = CreateDraftService(handler);

        var exception = await Assert.ThrowsAsync<AiDraftGenerationException>(() =>
            service.GenerateQuestionDraftAsync(
                Guid.NewGuid(),
                new GenerateQuestionDraftRequest(
                    TaskTypes.DatabaseQuerySchema,
                    "hard",
                    ["sql"]),
                sharedPrototypeReference: null,
                sortOrder: 1,
                CancellationToken.None));

        Assert.Contains("required task type is 'database_query_schema'", exception.Message);
        Assert.DoesNotContain("missing test code", exception.Message);
    }

    [Fact]
    public async Task Generate_question_draft_merges_canonical_files_before_starter_file_validation()
    {
        var handler = new CapturingHandler(OpenAiResponse(AdvancedSqlTaskContent(
            starterFiles: new Dictionary<string, string>
            {
                ["solution.sql"] = "-- Implement the Todo task reporting queries."
            })));
        var service = CreateDraftService(handler);

        var question = await service.GenerateQuestionDraftAsync(
            Guid.NewGuid(),
            new GenerateQuestionDraftRequest(
                TaskTypes.DatabaseQuerySchema,
                "hard",
                ["sql"]),
            sharedPrototypeReference: null,
            sortOrder: 1,
            CancellationToken.None);

        var starterCode = JsonDocumentSerializer.DeserializeStarterCode(question.StarterCodeJson);
        Assert.Contains("schema.sql", starterCode["sql"].Keys);
        Assert.Contains("seed.sql", starterCode["sql"].Keys);
        Assert.Contains("solution.sql", starterCode["sql"].Keys);
        Assert.Contains(
            "at least 2 editable starter files for every supported language",
            ReadSystemPrompt(handler.CapturedBodies.First()));
    }

    [Fact]
    public async Task Generate_question_draft_accepts_advanced_multi_file_task()
    {
        var handler = new CapturingHandler(OpenAiResponse(AdvancedSqlTaskContent(
            starterFiles: new Dictionary<string, string>
            {
                ["schema.sql"] = "CREATE TABLE todo_tasks (id INTEGER PRIMARY KEY, completed INTEGER NOT NULL);",
                ["seed.sql"] = "INSERT INTO todo_tasks (id, completed) VALUES (1, 0), (2, 1);",
                ["solution.sql"] = "-- Implement the transaction-safe Todo task reporting queries."
            })));
        var service = CreateDraftService(handler);

        var question = await service.GenerateQuestionDraftAsync(
            Guid.NewGuid(),
            new GenerateQuestionDraftRequest(
                TaskTypes.DatabaseQuerySchema,
                "hard",
                ["sql"]),
            sharedPrototypeReference: null,
            sortOrder: 1,
            CancellationToken.None);

        var starterCode = JsonDocumentSerializer.DeserializeStarterCode(question.StarterCodeJson);
        Assert.Equal(3, starterCode["sql"].Count);
        Assert.Contains("CREATE TABLE IF NOT EXISTS todos", starterCode["sql"]["schema.sql"]);
        Assert.Contains("canonical Todo schema", starterCode["sql"]["solution.sql"]);
        Assert.Equal(PrototypeDefaults.TodoListReference, question.StarterPrototypeReference);
        var benchmark = TaskAiUsageBenchmarkFactory.Read(
            question.GradingConfigurationJson,
            question.TaskType,
            question.Difficulty);
        Assert.Equal(TaskAiUsageBenchmarkFactory.Version, benchmark.Version);
        Assert.Equal(1375, benchmark.ReferenceTotalTokens);
        var metadata = JsonDocumentSerializer.Deserialize(
            question.StarterFilesMetadataJson,
            new Dictionary<string, Dictionary<string, string>>());
        Assert.Equal("editable", metadata["sql"]["schema.sql"]);
        Assert.Equal("editable", metadata["sql"]["seed.sql"]);
        Assert.Equal("editable", metadata["sql"]["solution.sql"]);
        Assert.Equal(4, question.TestCases.Count);
    }

    [Fact]
    public void Canonical_prototype_source_overwrites_generated_base_files_and_keeps_task_files()
    {
        var source = new CanonicalPrototypeSource();

        var starterCode = source.ApplyCanonicalFiles(
            new Dictionary<string, Dictionary<string, string>>
            {
                ["html"] = new()
                {
                    ["index.html"] = "<p>LLM replacement</p>",
                    ["task-helper.js"] = "// task-specific helper"
                }
            },
            ["html"]);

        Assert.Contains("Todo List App", starterCode["html"]["index.html"]);
        Assert.Contains("const todos", starterCode["html"]["app.js"]);
        Assert.Equal("// task-specific helper", starterCode["html"]["task-helper.js"]);
    }

    [Fact]
    public async Task Generate_assessment_draft_includes_public_and_hidden_test_cases_for_every_task()
    {
        var handler = new CapturingHandler(OpenAiResponse(AdvancedSqlTaskContent(
            starterFiles: new Dictionary<string, string>
            {
                ["schema.sql"] = "CREATE TABLE todo_tasks (id INTEGER PRIMARY KEY, completed INTEGER NOT NULL);",
                ["seed.sql"] = "INSERT INTO todo_tasks (id, completed) VALUES (1, 0), (2, 1);",
                ["solution.sql"] = "-- Implement the transaction-safe Todo task reporting queries."
            })));
        var service = CreateDraftService(handler);

        var questions = await service.GenerateAssessmentDraftAsync(
            Guid.NewGuid(),
            new AssessmentRequest(
                "Todo data integrity",
                "Generate a transaction-safe Todo database assessment.",
                50,
                AssessmentStatuses.Draft,
                true,
                TaskTypeCounts: new Dictionary<string, int>
                {
                    [TaskTypes.FrontendUiExtension] = 0,
                    [TaskTypes.RestApiDevelopment] = 0,
                    [TaskTypes.DatabaseQuerySchema] = 1,
                    [TaskTypes.BugFix] = 0
                },
                Difficulty: "hard"),
            CancellationToken.None);

        var question = Assert.Single(questions);
        Assert.Equal(2, question.TestCases.Count(testCase => testCase.Visibility == TestCaseVisibilities.Public));
        Assert.Equal(2, question.TestCases.Count(testCase => testCase.Visibility == TestCaseVisibilities.Hidden));
        Assert.All(question.TestCases, testCase =>
        {
            var testCode = JsonDocumentSerializer.Deserialize(
                testCase.TestCodeJson,
                new Dictionary<string, string>());
            Assert.True(testCode.TryGetValue("sql", out var sqlTestCode));
            Assert.False(string.IsNullOrWhiteSpace(sqlTestCode));
            Assert.Equal(AuthoringSources.LlmGenerated, testCase.AuthoringSource);
        });
    }

    [Fact]
    public async Task Generate_question_draft_rejects_verbose_but_tutorial_level_task()
    {
        var shallowDescription = string.Join(" ", Enumerable.Repeat(
            "Build a polished Todo task progress bar that updates when a checkbox changes and make the layout responsive with clear colors and helpful labels.",
            8));
        var handler = new CapturingHandler(OpenAiResponse(AdvancedSqlTaskContent(
            starterFiles: new Dictionary<string, string>
            {
                ["schema.sql"] = "CREATE TABLE tasks (id INTEGER PRIMARY KEY, completed INTEGER NOT NULL);",
                ["seed.sql"] = "INSERT INTO tasks (id, completed) VALUES (1, 0), (2, 1);",
                ["solution.sql"] = "-- Calculate the percentage of completed tasks."
            },
            description: shallowDescription)));
        var service = CreateDraftService(handler);

        var exception = await Assert.ThrowsAsync<AiDraftGenerationException>(() =>
            service.GenerateQuestionDraftAsync(
                Guid.NewGuid(),
                new GenerateQuestionDraftRequest(
                    TaskTypes.DatabaseQuerySchema,
                    "easy",
                    ["sql"]),
                sharedPrototypeReference: null,
                sortOrder: 1,
                CancellationToken.None));

        Assert.Contains("still tutorial-level", exception.Message);
    }

    [Fact]
    public async Task Generate_question_draft_rejects_problem_statements_over_150_words()
    {
        var verboseDescription = string.Join(" ", Enumerable.Repeat(
            "Todo transactions require concurrency rollback audit persistence accessibility validation and deterministic dependency handling.",
            16));
        var handler = new CapturingHandler(OpenAiResponse(AdvancedSqlTaskContent(
            starterFiles: new Dictionary<string, string>
            {
                ["schema.sql"] = "CREATE TABLE todo_tasks (id INTEGER PRIMARY KEY, completed INTEGER NOT NULL);",
                ["seed.sql"] = "INSERT INTO todo_tasks (id, completed) VALUES (1, 0), (2, 1);",
                ["solution.sql"] = "-- Implement transaction-safe Todo reporting."
            },
            description: verboseDescription)));
        var service = CreateDraftService(handler);

        var exception = await Assert.ThrowsAsync<AiDraftGenerationException>(() =>
            service.GenerateQuestionDraftAsync(
                Guid.NewGuid(),
                new GenerateQuestionDraftRequest(
                    TaskTypes.DatabaseQuerySchema,
                    "hard",
                    ["sql"]),
                sharedPrototypeReference: null,
                sortOrder: 1,
                CancellationToken.None));

        Assert.Contains("no more than 150 words", exception.Message);
    }

    [Fact]
    public async Task Generate_question_draft_retries_with_validation_feedback()
    {
        var shallowDescription = string.Join(" ", Enumerable.Repeat(
            "Build a polished Todo task progress bar that updates when a checkbox changes and make the layout responsive with clear colors and helpful labels.",
            8));
        var starterFiles = new Dictionary<string, string>
        {
            ["schema.sql"] = "CREATE TABLE todo_tasks (id INTEGER PRIMARY KEY, completed INTEGER NOT NULL);",
            ["seed.sql"] = "INSERT INTO todo_tasks (id, completed) VALUES (1, 0), (2, 1);",
            ["solution.sql"] = "-- Implement the transaction-safe Todo task reporting queries."
        };
        var handler = new SequencedHandler(
            OpenAiResponse(AdvancedSqlTaskContent(starterFiles, shallowDescription)),
            OpenAiResponse(AdvancedSqlTaskContent(starterFiles)));
        var service = CreateDraftService(handler);

        var question = await service.GenerateQuestionDraftAsync(
            Guid.NewGuid(),
            new GenerateQuestionDraftRequest(
                TaskTypes.DatabaseQuerySchema,
                "hard",
                ["sql"]),
            sharedPrototypeReference: null,
            sortOrder: 1,
            CancellationToken.None);

        Assert.Equal("Transaction-Safe Todo Audit Reconciliation", question.Title);
        Assert.Equal(3, handler.CallCount);
        Assert.Contains("previous draft was rejected", handler.CapturedBodies[1]);
        Assert.Contains("still tutorial-level", handler.CapturedBodies[1]);
        Assert.Contains("exact vocabulary", handler.CapturedBodies[1]);
    }

    [Fact]
    public async Task Generate_question_draft_allows_more_than_three_correction_attempts()
    {
        var shallowDescription = string.Join(" ", Enumerable.Repeat(
            "Build a polished Todo task progress bar that updates when a checkbox changes and make the layout responsive with clear colors and helpful labels.",
            8));
        var starterFiles = new Dictionary<string, string>
        {
            ["schema.sql"] = "CREATE TABLE todo_tasks (id INTEGER PRIMARY KEY, completed INTEGER NOT NULL);",
            ["seed.sql"] = "INSERT INTO todo_tasks (id, completed) VALUES (1, 0), (2, 1);",
            ["solution.sql"] = "-- Implement the transaction-safe Todo task reporting queries."
        };
        var shallowResponse = OpenAiResponse(AdvancedSqlTaskContent(starterFiles, shallowDescription));
        var handler = new SequencedHandler(
            shallowResponse,
            shallowResponse,
            shallowResponse,
            OpenAiResponse(AdvancedSqlTaskContent(starterFiles)));
        var service = CreateDraftService(handler);

        var question = await service.GenerateQuestionDraftAsync(
            Guid.NewGuid(),
            new GenerateQuestionDraftRequest(
                TaskTypes.DatabaseQuerySchema,
                "hard",
                ["sql"]),
            sharedPrototypeReference: null,
            sortOrder: 1,
            CancellationToken.None);

        Assert.Equal("Transaction-Safe Todo Audit Reconciliation", question.Title);
        Assert.Equal(5, handler.CallCount);
    }

    [Fact]
    public async Task Generate_question_draft_rejects_unrelated_product_domains()
    {
        var unrelatedDescription = string.Join(" ",
        [
            "Context: A banking platform requires a transaction reconciliation engine with concurrency control and audit logging.",
            "Deliverables include schema migrations, idempotent payment processing, rollback behavior, caching, and conflict resolution.",
            "Functional requirements cover account transfers, authorization, duplicate payment detection, transaction history, window-function reports, and failure recovery.",
            "Constraints require stable banking APIs, consistent errors, safe retries, and backward compatibility.",
            "Edge cases include concurrent transfers, missing account owners, duplicate requests, partial failures, and null references.",
            "Acceptance criteria require all modules and public and hidden tests to pass without changing existing payment interfaces."
        ]);
        var starterFiles = new Dictionary<string, string>
        {
            ["schema.sql"] = "CREATE TABLE bank_accounts (id INTEGER PRIMARY KEY, balance INTEGER NOT NULL);",
            ["seed.sql"] = "INSERT INTO bank_accounts (id, balance) VALUES (1, 100);",
            ["solution.sql"] = "-- Implement banking reconciliation."
        };
        var handler = new CapturingHandler(OpenAiResponse(
            AdvancedSqlTaskContent(starterFiles, unrelatedDescription)));
        var service = CreateDraftService(handler);

        var exception = await Assert.ThrowsAsync<AiDraftGenerationException>(() =>
            service.GenerateQuestionDraftAsync(
                Guid.NewGuid(),
                new GenerateQuestionDraftRequest(
                    TaskTypes.DatabaseQuerySchema,
                    "hard",
                    ["sql"]),
                sharedPrototypeReference: null,
                sortOrder: 1,
                CancellationToken.None));

        Assert.Contains("not anchored to the default Todo List prototype", exception.Message);
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

    private static Dictionary<string, int> RequiredTaskTypeCounts(int count)
    {
        return new Dictionary<string, int>
        {
            [TaskTypes.FrontendUiExtension] = count,
            [TaskTypes.RestApiDevelopment] = count,
            [TaskTypes.DatabaseQuerySchema] = count,
            [TaskTypes.BugFix] = count
        };
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

    private static string BaselineOpenAiResponse()
    {
        return OpenAiResponse(JsonSerializer.Serialize(new
        {
            goal = "Implement the requested Todo behavior.",
            code_context = "The Todo starter files define shared contracts.",
            observed_behavior = "The current implementation is incomplete.",
            constraint = "Preserve existing Todo APIs and validation behavior.",
            standard_steps = new[]
            {
                new
                {
                    purpose = "Inspect the Todo contract",
                    minimal_input = "Summarize the public Todo API contract.",
                    public_verification = "Confirm public tests still pass."
                },
                new
                {
                    purpose = "Implement the missing behavior",
                    minimal_input = "Suggest the smallest Todo API change.",
                    public_verification = "Run the public test cases."
                }
            }
        }));
    }

    private static string ReadLastUserPrompt(string requestBody)
    {
        using var document = JsonDocument.Parse(requestBody);
        return document.RootElement
            .GetProperty("messages")
            .EnumerateArray()
            .Where(message => message.GetProperty("role").GetString() == "user")
            .Select(message => message.GetProperty("content").GetString() ?? "")
            .Last();
    }

    private static string ReadSystemPrompt(string requestBody)
    {
        using var document = JsonDocument.Parse(requestBody);
        return document.RootElement
            .GetProperty("messages")
            .EnumerateArray()
            .Single(message => message.GetProperty("role").GetString() == "system")
            .GetProperty("content")
            .GetString() ?? "";
    }

    private static string AdvancedSqlTaskContent(
        Dictionary<string, string> starterFiles,
        string? description = null,
        object[]? testCases = null)
    {
        description ??= string.Join(" ",
        [
            "Context: The default Todo List application needs a transaction-safe task history and reconciliation module spanning schema, seed data, and reporting logic.",
            "Complete the supplied SQL files while preserving public todo, assignee, dependency, and audit view contracts.",
            "Functional requirements: enforce task ownership; prevent invalid completion transitions; record immutable todo audit entries; calculate running completion metrics with window functions; produce daily assignee summaries; identify duplicate update requests idempotently; support nullable due dates; and expose failed dependency reconciliations.",
            "Use portable SQL, preserve referential integrity, avoid data loss, and make migrations safe to rerun.",
            "Edge cases: duplicate task updates, null due dates, dependency cycles, concurrent completion changes, orphaned audit rows, and ties in timestamps must behave deterministically.",
            "Acceptance criteria: all public and hidden checks pass, constraints reject invalid todo writes, aggregates remain correct after retries, and existing Todo prototype consumers continue to query the original views without changes."
        ]);
        testCases ??= new object[]
        {
            BuildSqlTestCase("Public schema contracts", TestCaseVisibilities.Public, "schema.sql"),
            BuildSqlTestCase("Public reconciliation view", TestCaseVisibilities.Public, "solution.sql"),
            BuildSqlTestCase("Hidden idempotency constraints", TestCaseVisibilities.Hidden, "schema.sql"),
            BuildSqlTestCase("Hidden edge-case aggregation", TestCaseVisibilities.Hidden, "solution.sql")
        };

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
                    starter_prototype_reference = (string?)null,
                    problem_description_markdown = description,
                    language_constraints = new[] { "sql" },
                    starter_code = new Dictionary<string, Dictionary<string, string>>
                    {
                        ["sql"] = starterFiles
                    },
                    starter_files_metadata = new Dictionary<string, Dictionary<string, string>>
                    {
                        ["sql"] = starterFiles.Keys.ToDictionary(fileName => fileName, _ => "editable")
                    },
                    verification_metadata = new { primary_view = VerificationModes.DatabaseResultCheck },
                    grading_configuration = new { runner = "automated_tests", requires_student_install = "false" },
                    traceability_metadata = new { requirements = "REQ-18f,REQ-18g,REQ-18h,REQ-18i,REQ-18j" },
                    max_score = 25,
                    test_cases = testCases
                }
            }
        });
    }

    private static string AdvancedTypeScriptTaskContent()
    {
        var description = string.Join(" ",
        [
            "Context: The default Todo List API needs a TypeScript service layer that handles idempotency, optimistic locking, concurrency, validation, rollback, and conflict resolution for offline-created tasks.",
            "Complete the supplied Todo contracts while preserving existing create, update, and toggle semantics.",
            "Functional requirements: assign stable client request IDs, reject stale version tokens, keep pending offline additions visible until sync settles, roll back failed updates without losing descriptions, and return deterministic conflict errors.",
            "Edge cases include duplicate create retries, concurrent completion toggles, missing titles, stale versions, and transport failures.",
            "Acceptance criteria require all public and hidden tests to pass while existing Todo consumers keep their current imports."
        ]);

        var starterFiles = new Dictionary<string, string>
        {
            ["solution.ts"] = "import { TodoService } from './services';\nexport function solve(command: unknown): unknown { return new TodoService().execute(command); }\n",
            ["types.ts"] = "export interface Todo { id: string; title: string; description: string; completed: boolean; version: number; pending?: boolean; }\nexport interface TodoCommand { type: string; payload?: unknown; requestId?: string; version?: number; }\n",
            ["services.ts"] = "import type { Todo, TodoCommand } from './types';\nexport class TodoService { private todos: Todo[] = []; execute(_command: TodoCommand): unknown { throw new Error('Implement idempotent Todo conflict handling'); } }\n"
        };

        return JsonSerializer.Serialize(new
        {
            tasks = new[]
            {
                new
                {
                    title = "Idempotent TypeScript Todo Conflict API",
                    task_type = TaskTypes.RestApiDevelopment,
                    difficulty = "hard",
                    verification_mode = VerificationModes.AutomatedTest,
                    starter_prototype_reference = (string?)null,
                    problem_description_markdown = description,
                    language_constraints = new[] { "typescript" },
                    starter_code = new Dictionary<string, Dictionary<string, string>>
                    {
                        ["typescript"] = starterFiles
                    },
                    starter_files_metadata = new Dictionary<string, Dictionary<string, string>>
                    {
                        ["typescript"] = starterFiles.Keys.ToDictionary(fileName => fileName, _ => "editable")
                    },
                    verification_metadata = new { primary_view = VerificationModes.AutomatedTest },
                    grading_configuration = new { runner = "automated_tests", requires_student_install = "false" },
                    traceability_metadata = new { requirements = "REQ-18f,REQ-18g,REQ-18h,REQ-18i,REQ-18j" },
                    max_score = 25,
                    test_cases = new object[]
                    {
                        BuildTypeScriptTestCase("Public: Optimistic add updates UI immediately", TestCaseVisibilities.Public, "solution.ts"),
                        BuildTypeScriptTestCase("Public: Duplicate request is idempotent", TestCaseVisibilities.Public, "services.ts"),
                        BuildTypeScriptTestCase("Hidden: Stale version reports conflict", TestCaseVisibilities.Hidden, "types.ts"),
                        BuildTypeScriptTestCase("Hidden: Failed sync rolls back pending state", TestCaseVisibilities.Hidden, "services.ts")
                    }
                }
            }
        });
    }

    private static object BuildRawSqlTestCase(string name, string visibility, string sql)
    {
        return new
        {
            name,
            visibility,
            test_code = new Dictionary<string, string>
            {
                ["sql"] = sql
            },
            traceability_metadata = new { requirements = "REQ-52,REQ-53" }
        };
    }

    private static object BuildSqlTestCase(string name, string visibility, string fileName)
    {
        return new
        {
            name,
            visibility,
            test_code = new Dictionary<string, string>
            {
                ["sql"] = $"const fs = require('fs'); test('{name}', () => expect(fs.readFileSync('{fileName}', 'utf8').trim().length).toBeGreaterThan(10));"
            },
            traceability_metadata = new { requirements = "REQ-52,REQ-53" }
        };
    }

    private static object BuildTypeScriptTestCase(string name, string visibility, string fileName)
    {
        return new
        {
            name,
            visibility,
            test_code = new Dictionary<string, string>
            {
                ["typescript"] = $"const fs = require('fs'); test('{name}', () => expect(fs.readFileSync('{fileName}', 'utf8').toLowerCase()).toContain('todo'));"
            },
            traceability_metadata = new { requirements = "REQ-52,REQ-53" }
        };
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
        private readonly string responseBody;

        public CapturingHandler(string responseBody)
        {
            this.responseBody = responseBody;
        }

        public string CapturedBody { get; private set; } = "";
        public ConcurrentQueue<string> CapturedBodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CapturedBody = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);
            CapturedBodies.Enqueue(CapturedBody);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class SequencedHandler : HttpMessageHandler
    {
        private readonly Queue<string> responseBodies;

        public SequencedHandler(params string[] responseBodies)
        {
            this.responseBodies = new Queue<string>(responseBodies);
        }

        public int CallCount => CapturedBodies.Count;

        public List<string> CapturedBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CapturedBodies.Add(request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBodies.Dequeue(), Encoding.UTF8, "application/json")
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
