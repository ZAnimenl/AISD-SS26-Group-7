using System.Text;
using Backend.Api;
using Backend.Domain;
using Backend.Services;
using Backend.Services.Grading;

namespace OjSharp.Tests.ApiContractTests;

public sealed class CodeEvaluationServiceTests
{
    [Fact]
    public async Task Evaluation_runs_all_test_cases_before_scoring()
    {
        var runner = new RecordingCodeRunner([
            new CodeRunResult("1", null, 0, false),
            new CodeRunResult("wrong", null, 0, false),
            new CodeRunResult(string.Empty, "TypeError: boom", 1, false)
        ]);
        var service = new CodeEvaluationService(runner);
        var testCases = new[]
        {
            TestCase("first", "input-1", "1"),
            TestCase("second", "input-2", "2"),
            TestCase("third", "input-3", "3")
        };

        var result = await service.EvaluateAsync(Guid.NewGuid(), testCases, new Dictionary<string, string> { ["solution.py"] = "code" }, "python", CancellationToken.None);

        Assert.Equal(3, runner.TestCaseNames.Count);
        Assert.Equal(["first", "second", "third"], runner.TestCaseNames);
        Assert.Equal(ExecutionStatuses.RuntimeError, result.Status);
        Assert.Equal(2, result.TestResults.Count(testResult => testResult.Passed));
        Assert.Equal(67, service.CalculateScore(100, result.TestResults));
    }

    [Fact]
    public async Task Python_solution_is_evaluated_against_test_case_output()
    {
        var runner = new RecordingCodeRunner([
            new CodeRunResult("6", null, 0, false),
            new CodeRunResult("12", null, 0, false)
        ]);
        var service = new CodeEvaluationService(runner);
        var testCases = new[]
        {
            TestCase("public", "[1,2,3]", "6"),
            TestCase("hidden", "[-3,5,10]", "12")
        };

        var result = await service.EvaluateAsync(
            Guid.NewGuid(),
            testCases,
            new Dictionary<string, string> { ["solution.py"] = "def solve(arr):\n    return sum(arr)\n" },
            "python",
            CancellationToken.None);

        Assert.Equal(ExecutionStatuses.Passed, result.Status);
        Assert.All(result.TestResults, testResult => Assert.True(testResult.Passed));
        Assert.Equal(50, service.CalculateScore(50, result.TestResults));
    }

    [Fact]
    public void Score_is_based_on_passed_test_case_ratio()
    {
        var service = new CodeEvaluationService(new RecordingCodeRunner([]));
        var results = new[]
        {
            TestResult(true),
            TestResult(false),
            TestResult(true),
            TestResult(false)
        };

        var score = service.CalculateScore(50, results);

        Assert.Equal(25, score);
    }

    [Fact]
    public async Task Timed_out_execution_returns_time_limit_status()
    {
        var runner = new RecordingCodeRunner([
            new CodeRunResult(string.Empty, "Execution timed out.", 1, true)
        ]);
        var service = new CodeEvaluationService(runner);

        var result = await service.EvaluateAsync(
            Guid.NewGuid(),
            [TestCase("timeout", "input", "output")],
            new Dictionary<string, string> { ["solution.py"] = "while True:\n    pass\n" },
            "python",
            CancellationToken.None);

        Assert.Equal(ExecutionStatuses.TimeLimitExceeded, result.Status);
        Assert.Equal(ExecutionStatuses.TimeLimitExceeded, result.TestResults[0].Status);
    }

    [Fact]
    public async Task Todo_summary_preview_reports_internal_error_when_grader_is_unavailable()
    {
        var runner = new RecordingCodeRunner([
            new CodeRunResult(string.Empty, "Grader container unavailable: Connection failed", 1, false)
        ]);
        var service = new CodeEvaluationService(runner);

        var result = await service.EvaluateAsync(
            Guid.NewGuid(),
            [TodoSummaryTestCase()],
            new Dictionary<string, string>
            {
                ["TodoSummaryPanel.py"] = """
                def build_summary(todos):
                    total = len(todos)
                    completed = sum(1 for todo in todos if todo.get("completed") is True)
                    pending = total - completed
                    message = "All tasks complete" if total > 0 and completed == total else ""
                    return {"total": total, "completed": completed, "pending": pending, "message": message}

                def render_summary_panel(todos):
                    summary = build_summary(todos)
                    return f'<section><h2>Todo Summary</h2><p>Total: {summary["total"]}</p></section>'
                """
            },
            "python",
            CancellationToken.None);

        Assert.Equal(ExecutionStatuses.InternalError, result.Status);
        Assert.Equal(ExecutionStatuses.InternalError, result.TestResults[0].Status);
        Assert.False(result.TestResults[0].Passed);
        Assert.Contains("Run environment unavailable", result.Stderr);
    }

    [Fact]
    public async Task Non_platform_task_reports_internal_error_when_grader_is_unavailable()
    {
        var runner = new RecordingCodeRunner([
            new CodeRunResult(string.Empty, "Grader container unavailable: Connection failed", 1, false)
        ]);
        var service = new CodeEvaluationService(runner);

        var result = await service.EvaluateAsync(
            Guid.NewGuid(),
            [TestCase("ordinary", "input", "output")],
            new Dictionary<string, string> { ["solution.py"] = "def solve():\n    return 1\n" },
            "python",
            CancellationToken.None);

        Assert.Equal(ExecutionStatuses.InternalError, result.Status);
        Assert.Equal(ExecutionStatuses.InternalError, result.TestResults[0].Status);
        Assert.Contains("Run environment unavailable", result.Stderr);
    }

    [Fact]
    public void Grader_commands_use_pytest_jest_and_typescript_compile_step()
    {
        var factory = new GraderCommandFactory();

        var pythonCommand = factory.Create(GradingLanguage.Python);
        var javascriptCommand = factory.Create(GradingLanguage.JavaScript);
        var typeScriptCommand = factory.Create(GradingLanguage.TypeScript);

        Assert.Contains("TODO_DATABASE_PATH=\"$PWD/todos.db\"", string.Join(" ", pythonCommand));
        Assert.Contains("pytest", string.Join(" ", pythonCommand));
        Assert.Contains("no:cacheprovider", string.Join(" ", pythonCommand));
        Assert.Contains("jest", javascriptCommand);
        Assert.Contains("--env=jsdom", javascriptCommand);
        Assert.Contains("tsc solution.ts", string.Join(" ", typeScriptCommand));
        Assert.Contains("--env=jsdom", string.Join(" ", typeScriptCommand));
        Assert.Contains("jest", string.Join(" ", typeScriptCommand));
    }

    [Fact]
    public void Grader_test_files_store_admin_test_code_for_python_javascript_and_typescript()
    {
        var factory = new GradingTestFileFactory();
        var pythonDirectory = Directory.CreateTempSubdirectory("ojsharp-python-test-");
        var javascriptDirectory = Directory.CreateTempSubdirectory("ojsharp-javascript-test-");
        var typeScriptDirectory = Directory.CreateTempSubdirectory("ojsharp-typescript-test-");
        try
        {
            factory.Write(pythonDirectory.FullName, new Dictionary<string, string> { ["solution.py"] = "def solve(value):\n    return value\n" }, "from solution import solve\n", GradingLanguage.Python);
            factory.Write(javascriptDirectory.FullName, new Dictionary<string, string> { ["solution.js"] = "function solve(value) {\n  return value;\n}\nmodule.exports = { solve };\n" }, "const { solve } = require(\"./solution.js\");\n", GradingLanguage.JavaScript);
            factory.Write(typeScriptDirectory.FullName, new Dictionary<string, string> { ["solution.ts"] = "function solve(value: string): string {\n  return value;\n}\n" }, "const solve = globalThis.__ojsharpSolve;\n", GradingLanguage.TypeScript);

        Assert.Contains("from solution import solve", File.ReadAllText(Path.Combine(pythonDirectory.FullName, "test_solution.py")));
        Assert.Contains("require(\"./solution.js\")", File.ReadAllText(Path.Combine(javascriptDirectory.FullName, "solution.test.js")));
        Assert.True(File.Exists(Path.Combine(typeScriptDirectory.FullName, "solution.ts")));
        }
        finally
        {
            pythonDirectory.Delete(true);
            javascriptDirectory.Delete(true);
            typeScriptDirectory.Delete(true);
        }
    }

    [Fact]
    public void Python_grader_test_files_align_audit_log_model_with_raw_sql_table_name()
    {
        var factory = new GradingTestFileFactory();
        var directory = Directory.CreateTempSubdirectory("ojsharp-audit-table-test-");
        try
        {
            factory.Write(
                directory.FullName,
                new Dictionary<string, string>
                {
                    ["models.py"] = """
                    from peewee import AutoField, ForeignKeyField, Model, SqliteDatabase

                    db = SqliteDatabase(":memory:")

                    class Todo(Model):
                        id = AutoField()

                        class Meta:
                            database = db

                    class AuditLog(Model):
                        audit_id = AutoField()
                        todo = ForeignKeyField(Todo, backref='audit_logs')

                        class Meta:
                            database = db
                    """
                },
                "from models import database\n\ndef test_audit_table_name():\n    database.execute_sql(\"select count(*) from audit_log\")\n",
                GradingLanguage.Python);

            var models = File.ReadAllText(Path.Combine(directory.FullName, "models.py"));
            Assert.Contains("AuditLog._meta.table_name = \"audit_log\"", models);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void Python_grader_test_files_bootstrap_todo_table_for_generated_direct_model_tests()
    {
        var factory = new GradingTestFileFactory();
        var directory = Directory.CreateTempSubdirectory("ojsharp-todo-bootstrap-test-");
        try
        {
            factory.Write(
                directory.FullName,
                new Dictionary<string, string>
                {
                    ["models.py"] = """
                    from peewee import AutoField, Model, SqliteDatabase

                    db = SqliteDatabase(":memory:")

                    class Todo(Model):
                        id = AutoField()

                        class Meta:
                            database = db
                    """
                },
                "from models import Todo\n\ndef test_direct_create():\n    Todo.create()\n",
                GradingLanguage.Python);

            var models = File.ReadAllText(Path.Combine(directory.FullName, "models.py"));
            Assert.Contains("__ojsharp_ensure_todo_table()", models);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void Python_grader_test_files_bootstrap_peewee_tables_for_fastapi_testclient_tests()
    {
        var factory = new GradingTestFileFactory();
        var directory = Directory.CreateTempSubdirectory("ojsharp-fastapi-bootstrap-test-");
        try
        {
            factory.Write(
                directory.FullName,
                new Dictionary<string, string>
                {
                    ["models.py"] = """
                    from peewee import AutoField, CharField, ForeignKeyField, Model, SqliteDatabase

                    db = SqliteDatabase(":memory:")

                    class Todo(Model):
                        id = AutoField()
                        title = CharField()

                        class Meta:
                            database = db

                    class AuditLog(Model):
                        id = AutoField()
                        todo = ForeignKeyField(Todo, backref="audit_logs")

                        class Meta:
                            database = db
                    """
                },
                "from fastapi.testclient import TestClient\nfrom main import app\nclient = TestClient(app)\n",
                GradingLanguage.Python);

            var models = File.ReadAllText(Path.Combine(directory.FullName, "models.py"));
            Assert.Contains("__ojsharp_create_all_peewee_tables()", models);
            Assert.Contains("cache=shared", models);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void Python_grader_test_files_add_legacy_service_method_aliases_for_generated_tests()
    {
        var factory = new GradingTestFileFactory();
        var directory = Directory.CreateTempSubdirectory("ojsharp-service-alias-test-");
        try
        {
            factory.Write(
                directory.FullName,
                new Dictionary<string, string>
                {
                    ["services.py"] = """
                    class TodoService:
                        def get_todo_by_id(self, todo_id):
                            return todo_id

                        def toggle_todo_completion(self, todo_id):
                            return todo_id
                    """
                },
                "from services import TodoService\n\ndef test_service_aliases():\n    service = TodoService()\n    service.get_todo(1)\n    service.toggle_todo(1)\n",
                GradingLanguage.Python);

            var services = File.ReadAllText(Path.Combine(directory.FullName, "services.py"));
            Assert.Contains("TodoService.get_todo = TodoService.get_todo_by_id", services);
            Assert.Contains("TodoService.toggle_todo = TodoService.toggle_todo_completion", services);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void Python_grader_test_files_provide_legacy_sqlite_helpers_for_generated_tests()
    {
        var factory = new GradingTestFileFactory();
        var directory = Directory.CreateTempSubdirectory("ojsharp-sqlite-helper-test-");
        try
        {
            factory.Write(
                directory.FullName,
                new Dictionary<string, string>
                {
                    ["models.py"] = """
                    from peewee import AutoField, Model, SqliteDatabase

                    db = SqliteDatabase(":memory:")

                    class Todo(Model):
                        id = AutoField()

                        class Meta:
                            database = db
                    """
                },
                "from models import init_db, get_db\n\ndef test_helpers():\n    init_db()\n    assert get_db() is not None\n",
                GradingLanguage.Python);

            var models = File.ReadAllText(Path.Combine(directory.FullName, "models.py"));
            Assert.Contains("def init_db():", models);
            Assert.Contains("def get_db():", models);
            Assert.Contains("Todo._meta.table_name = \"todos\"", models);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void Grader_test_files_create_legacy_pascal_case_aliases_for_snake_case_starter_files()
    {
        var factory = new GradingTestFileFactory();
        var directory = Directory.CreateTempSubdirectory("ojsharp-alias-test-");
        try
        {
            factory.Write(
                directory.FullName,
                new Dictionary<string, string>
                {
                    ["todo_summary_panel.py"] = "def render_summary_panel(todos):\n    return ''\n",
                    ["already_named.py"] = "VALUE = 1\n",
                    ["TodoSummaryPanel.py"] = "EXPLICIT = True\n"
                },
                "from TodoSummaryPanel import render_summary_panel\n",
                GradingLanguage.Python);

            Assert.True(File.Exists(Path.Combine(directory.FullName, "TodoSummaryPanel.py")));
            Assert.Contains("EXPLICIT = True", File.ReadAllText(Path.Combine(directory.FullName, "TodoSummaryPanel.py")));
            Assert.True(File.Exists(Path.Combine(directory.FullName, "AlreadyNamed.py")));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void Html_grader_test_files_initialize_dom_and_websocket_dependencies()
    {
        var factory = new GradingTestFileFactory();
        var directory = Directory.CreateTempSubdirectory("ojsharp-html-test-");
        try
        {
            factory.Write(
                directory.FullName,
                new Dictionary<string, string>
                {
                    ["index.html"] = "<!doctype html><html><body><textarea id=\"doc\"></textarea><script src=\"app.js\"></script></body></html>",
                    ["app.js"] = "document.getElementById('doc').addEventListener('input', () => {});"
                },
                "const dom = new JSDOM('<main></main>');\nindexedDB.open('todo');\nrequire('./app.js');\nws.send({ type: 'insert' });\ndocument.getElementById('doc').value = 'Hello';\nconst saved = JSON.parse(localStorage.getItem('docState'));\n",
                GradingLanguage.JavaScript,
                isHtmlWorkspace: true);

            var testFile = File.ReadAllText(Path.Combine(directory.FullName, "solution.test.js"));
            var setupFile = File.ReadAllText(Path.Combine(directory.FullName, "jest.setup.js"));
            Assert.Contains("document.write(html)", testFile);
            Assert.Contains("globalThis.ws", testFile);
            Assert.Contains("require('./app.js')", testFile);
            Assert.Contains("test('generated public check'", testFile);
            Assert.Contains("localStorage.getItem('docState') ?? '{}'", testFile);
            Assert.Contains("global.JSDOM ??= JSDOM", setupFile);
            Assert.Contains("require('fake-indexeddb/auto')", setupFile);
            Assert.Contains("value?.indexedDB ?? value", setupFile);
            Assert.Contains("global.structuredClone ??=", setupFile);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void Html_grader_skips_generated_browser_dependency_shims_when_check_does_not_use_them()
    {
        var factory = new GradingTestFileFactory();
        var directory = Directory.CreateTempSubdirectory("ojsharp-html-fast-test-");
        try
        {
            factory.Write(
                directory.FullName,
                new Dictionary<string, string>
                {
                    ["index.html"] = "<!doctype html><main>Todo</main>",
                    ["app.js"] = string.Empty
                },
                "expect(document.querySelector('main')).not.toBeNull();",
                GradingLanguage.JavaScript,
                isHtmlWorkspace: true);

            var setupFile = File.ReadAllText(Path.Combine(directory.FullName, "jest.setup.js"));
            Assert.DoesNotContain("require('fake-indexeddb/auto')", setupFile);
            Assert.DoesNotContain("global.JSDOM ??= JSDOM", setupFile);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void Html_grader_strips_generated_jsdom_boilerplate_that_replaces_loaded_page()
    {
        var factory = new GradingTestFileFactory();
        var directory = Directory.CreateTempSubdirectory("ojsharp-html-jsdom-test-");
        try
        {
            factory.Write(
                directory.FullName,
                new Dictionary<string, string>
                {
                    ["index.html"] = "<!doctype html><html><body><ul id=\"todo-list\"><li><input type=\"checkbox\"></li></ul><script src=\"app.js\"></script></body></html>",
                    ["app.js"] = ""
                },
                """
                const { JSDOM } = require('jsdom');
                const { window } = new JSDOM('<!DOCTYPE html><html><body><div id="app"></div></body></html>', { url: 'http://localhost' });
                const { document, navigator } = window;
                global.document = document;
                global.window = window;
                global.navigator = navigator;
                mockFetch.mockResolvedValueOnce({ json: () => Promise.resolve([]) });
                expect(document.querySelector('input[type="checkbox"]')).not.toBeNull();
                expect(document.querySelector('.todo-item')).not.toBeNull();
                expect(todos[0].title).toBe('Persist Test');
                """,
                GradingLanguage.JavaScript,
                isHtmlWorkspace: true);

            var testFile = File.ReadAllText(Path.Combine(directory.FullName, "solution.test.js"));
            Assert.DoesNotContain("new JSDOM", testFile);
            Assert.DoesNotContain("global.document = document", testFile);
            Assert.Contains("globalThis.__ojSharpFirstUncheckedCheckbox()", testFile);
            Assert.Contains("globalThis.__ojSharpLastTodoItem()", testFile);
            Assert.Contains("'new-todo-title': '#todo-title'", testFile);
            Assert.Contains("'#new-todo-title': '#todo-title'", testFile);
            Assert.Contains("test('generated public check', (ojSharpDone)", testFile);
            Assert.Contains("const done = (error) =>", testFile);
            Assert.Contains("globalThis.setTimeout = ojSharpSafeSetTimeout;", testFile);
            Assert.Contains("todos.some(todo => todo.title === 'Persist Test')", testFile);
            Assert.Contains("mockFetch.mockResolvedValue({ json: () => Promise.resolve([]) });", testFile);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void Sql_grader_wraps_raw_sql_test_code_in_jest_harness()
    {
        var factory = new GradingTestFileFactory();
        var directory = Directory.CreateTempSubdirectory("ojsharp-sql-raw-test-");
        try
        {
            factory.Write(
                directory.FullName,
                new Dictionary<string, string>
                {
                    ["schema.sql"] = "CREATE TABLE todos (id INTEGER PRIMARY KEY, title TEXT NOT NULL);",
                    ["seed.sql"] = "INSERT INTO todos (title) VALUES ('Seed');",
                    ["solution.sql"] = "CREATE TABLE IF NOT EXISTS audit_log (id INTEGER PRIMARY KEY);"
                },
                "SELECT name FROM sqlite_master WHERE type='table' AND name='audit_log';",
                GradingLanguage.Sql);

            var testFile = File.ReadAllText(Path.Combine(directory.FullName, "solution.test.js"));
            Assert.Contains("generated SQL check", testFile);
            Assert.Contains("spawnSync('python3'", testFile);
            Assert.False(testFile.TrimStart().StartsWith("SELECT name", StringComparison.Ordinal));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public async Task Docker_runner_rejects_unsupported_language_without_starting_container()
    {
        var runner = new DockerCodeRunner();

        var result = await runner.RunAsync(
            new Dictionary<string, string> { ["Solution.cs"] = "public class Solution {}" },
            "csharp",
            TestCase("unsupported", "1", "1"),
            CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unsupported language", result.Stderr);
    }

    [Fact]
    public void Docker_build_context_contains_grader_dockerfile()
    {
        using var context = DockerBuildContextFactory.Create();
        var header = new byte[512];

        Assert.Equal(512, context.Read(header));
        var headerText = Encoding.ASCII.GetString(header);
        using var reader = new StreamReader(context, Encoding.UTF8);
        var bodyText = reader.ReadToEnd();

        Assert.Contains("Dockerfile", headerText);
        Assert.Contains("pytest", bodyText);
        Assert.Contains("fastapi", bodyText);
        Assert.Contains("jest", bodyText);
        Assert.Contains("jest-environment-jsdom", bodyText);
        Assert.Contains("fake-indexeddb@6.2.5", bodyText);
        Assert.Contains("jest-fetch-mock@4.2.0", bodyText);
        Assert.Contains("typescript", bodyText);
        Assert.Contains("PYTHONDONTWRITEBYTECODE", bodyText);
    }

    [Fact]
    public void Browser_preview_for_javascript_html_starter_reads_html_entry()
    {
        var question = new Question
        {
            Id = Guid.NewGuid(),
            VerificationMode = VerificationModes.BrowserUiPreview,
            StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
            {
                ["javascript"] = new()
                {
                    ["index.html"] = "<!doctype html><html><head><link rel=\"stylesheet\" href=\"styles.css\"></head><body><button id=\"clearBtn\">Clear All</button><script src=\"app.js\"></script></body></html>",
                    ["styles.css"] = "button { color: blue; }\n",
                    ["app.js"] = "document.getElementById('clearBtn');\n"
                }
            }),
            VerificationMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>())
        };

        var testCase = InvokeBrowserPreviewTest(question, "javascript");
        var testCode = JsonDocumentSerializer.Deserialize(testCase.TestCodeJson, new Dictionary<string, string>())["javascript"];

        Assert.Contains("index.html", testCode);
        Assert.Contains("inlineLocalAssets", testCode);
        Assert.Contains("styles.css", question.StarterCodeJson);
        Assert.DoesNotContain("TodoSummaryPanel", testCode);
    }

    private static TestCase TestCase(string name, string input, string expectedOutput)
    {
        return new TestCase
        {
            Id = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            Name = name,
            Visibility = TestCaseVisibilities.Public,
            TestCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["python"] = $"def test_{name.Replace("-", "_")}():\n    assert True\n",
                ["javascript"] = $"test(\"{name}\", () => expect(true).toBe(true));\n",
                ["typescript"] = $"test(\"{name}\", () => expect(true).toBe(true));\n"
            })
        };
    }

    private static TestCase TodoSummaryTestCase()
    {
        return new TestCase
        {
            Id = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            Name = "Summary counts visible todos",
            Visibility = TestCaseVisibilities.Public,
            TestCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["python"] = """
                from TodoSummaryPanel import build_summary, render_summary_panel

                def test_summary_counts_visible_todos():
                    summary = build_summary([])
                    assert summary["total"] == 0
                    assert "Todo Summary" in render_summary_panel([])
                """
            })
        };
    }

    private static TestCase InvokeBrowserPreviewTest(Question question, string selectedLanguage)
    {
        var method = typeof(ExecutionEndpoints).GetMethod(
            "CreateBrowserPreviewTest",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<TestCase>(method!.Invoke(null, [question, selectedLanguage]));
    }

    private static TestCaseEvaluationResult TestResult(bool passed)
    {
        return new TestCaseEvaluationResult(
            "test",
            TestCaseVisibilities.Public,
            passed,
            string.Empty,
            null,
            passed ? ExecutionStatuses.Passed : ExecutionStatuses.Failed);
    }

    private sealed class RecordingCodeRunner(IReadOnlyList<CodeRunResult> results) : ICodeRunner
    {
        private int index;

        public List<string> TestCaseNames { get; } = [];

        public Task<CodeRunResult> RunAsync(Dictionary<string, string> files, string language, TestCase testCase, CancellationToken cancellationToken)
        {
            TestCaseNames.Add(testCase.Name);
            return Task.FromResult(results[index++]);
        }
    }
}
