using Backend.Domain;
using Backend.Services;
using Backend.Services.Grading;
using Docker.DotNet;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace OjSharp.Tests.ApiContractTests;

public sealed class DockerCodeRunnerIntegrationTests
{
    private const string SeedDatabaseConnectionString = "Host=localhost:5433;Database=ai_coding;Username=ai_coding;password=password";

    private static bool IsDockerAvailable()
    {
        try
        {
            var endpoint = DockerGraderContainer.ResolveDockerEndpoint(
                Environment.GetEnvironmentVariable("DOCKER_HOST"),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                OperatingSystem.IsWindows(),
                Path.Exists);

            using var client = new DockerClientConfiguration(new Uri(endpoint)).CreateClient();
            var task = client.System.PingAsync();
            task.Wait(1500);
            return task.IsCompletedSuccessfully;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsSeedDatabaseAvailable()
    {
        try
        {
            await using var connection = new Npgsql.NpgsqlConnection(SeedDatabaseConnectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static TestCase CreateTestCase(string pythonTestCode, string javascriptTestCode)
    {
        var testCodeMap = new Dictionary<string, string>
        {
            ["python"] = pythonTestCode,
            ["javascript"] = javascriptTestCode,
            ["typescript"] = javascriptTestCode
        };

        return new TestCase
        {
            Id = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            Name = "IntegrationTest",
            Visibility = TestCaseVisibilities.Public,
            TestCodeJson = JsonSerializer.Serialize(testCodeMap)
        };
    }

    [Fact]
    public async Task Python_successful_execution_returns_zero_exit_code()
    {
        if (!IsDockerAvailable())
        {
            // Skip test gracefully if Docker is not running
            return;
        }

        var runner = new DockerCodeRunner();
        var testCase = CreateTestCase(
            "from solution import solve\ndef test_solve():\n    assert solve(3, 4) == 7\n",
            ""
        );

        var result = await runner.RunAsync(
            new Dictionary<string, string> { ["solution.py"] = "def solve(a, b):\n    return a + b\n" },
            "python",
            testCase,
            CancellationToken.None
        );

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Null(result.Stderr);
        Assert.Contains("passed", result.Stdout);
    }

    [Fact]
    public async Task Canonical_fastapi_app_runs_with_testclient_without_student_installation()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var files = new CanonicalPrototypeSource()
            .ApplyCanonicalFiles(
                new Dictionary<string, Dictionary<string, string>>(),
                ["python"])["python"];
        var runner = new DockerCodeRunner();
        var testCase = CreateTestCase(
            """
            from fastapi.testclient import TestClient
            from main import app

            def test_root_endpoint_is_available():
                with TestClient(app) as client:
                    response = client.get("/")
                assert response.status_code == 200
                assert response.json() == {"message": "Todo API", "version": "1.0.0"}
            """,
            "");

        var result = await runner.RunAsync(files, "python", testCase, CancellationToken.None);

        Assert.True(result.ExitCode == 0, result.Stderr ?? result.Stdout);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task Python_runner_aliases_canonical_db_as_database_for_generated_tests()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var runner = new DockerCodeRunner();
        var testCase = CreateTestCase(
            """
            import models

            def test_generated_database_alias_is_available():
                assert models.database is models.db
            """,
            "");

        var result = await runner.RunAsync(
            new Dictionary<string, string>
            {
                ["models.py"] = """
                from peewee import SqliteDatabase

                db = SqliteDatabase(":memory:")

                class Todo:
                    class Meta:
                        database = db
                """
            },
            "python",
            testCase,
            CancellationToken.None);

        Assert.True(result.ExitCode == 0, result.Stderr ?? result.Stdout);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task Python_runner_provides_missing_migration_stub_for_generated_tasks()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var runner = new DockerCodeRunner();
        var testCase = CreateTestCase(
            """
            from migration import run_migration

            def test_generated_migration_module_can_be_imported():
                assert callable(run_migration)
            """,
            "");

        var result = await runner.RunAsync(
            new Dictionary<string, string>
            {
                ["models.py"] = "",
                ["migration.py"] = "# Generated starter forgot to define run_migration.\n"
            },
            "python",
            testCase,
            CancellationToken.None);

        Assert.True(result.ExitCode == 0, result.Stderr ?? result.Stdout);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task Python_runner_uses_run_local_sqlite_database_for_flattened_environment_file()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var runner = new DockerCodeRunner();
        var canonicalEnvironment = """
            import os
            from pathlib import Path

            DATABASE_PATH = os.getenv(
                "TODO_DATABASE_PATH",
                str(Path(__file__).resolve().parents[1] / "todos.db"),
            )
            """;

        var oldSchemaTest = CreateTestCase(
            """
            import models

            def test_old_schema_creates_shared_name():
                models.db.connect(reuse_if_open=True)
                models.db.create_tables([models.Todo], safe=True)
                models.Todo.create(title="old")
                assert models.Todo.select().count() == 1
                models.db.close()
            """,
            "");
        var oldSchemaResult = await runner.RunAsync(
            new Dictionary<string, string>
            {
                ["environment.py"] = canonicalEnvironment,
                ["models.py"] = """
                from peewee import AutoField, CharField, Model, SqliteDatabase
                from environment import DATABASE_PATH

                db = SqliteDatabase(DATABASE_PATH)

                class Todo(Model):
                    id = AutoField()
                    title = CharField()

                    class Meta:
                        database = db
                """
            },
            "python",
            oldSchemaTest,
            CancellationToken.None);

        Assert.True(oldSchemaResult.ExitCode == 0, oldSchemaResult.Stderr ?? oldSchemaResult.Stdout);

        var evolvedSchemaTest = CreateTestCase(
            """
            import models

            def test_new_schema_does_not_reuse_stale_todo_table():
                models.db.connect(reuse_if_open=True)
                models.db.create_tables([models.Todo], safe=True)
                todo = models.Todo.create(title="new", version=2)
                assert todo.version == 2
                models.db.close()
            """,
            "");
        var evolvedSchemaResult = await runner.RunAsync(
            new Dictionary<string, string>
            {
                ["environment.py"] = canonicalEnvironment,
                ["models.py"] = """
                from peewee import AutoField, CharField, IntegerField, Model, SqliteDatabase
                from environment import DATABASE_PATH

                db = SqliteDatabase(DATABASE_PATH)

                class Todo(Model):
                    id = AutoField()
                    title = CharField()
                    version = IntegerField(default=1)

                    class Meta:
                        database = db
                """
            },
            "python",
            evolvedSchemaTest,
            CancellationToken.None);

        Assert.True(evolvedSchemaResult.ExitCode == 0, evolvedSchemaResult.Stderr ?? evolvedSchemaResult.Stdout);
        Assert.False(evolvedSchemaResult.TimedOut);
    }

    [Fact]
    public async Task Python_runner_aligns_generated_audit_log_model_with_raw_sql_tests()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var runner = new DockerCodeRunner();
        var testCase = CreateTestCase(
            """
            from models import database
            from migration import run_migration

            def test_audit_log_table_is_created_with_expected_raw_name():
                run_migration()
                cursor = database.execute_sql("SELECT name FROM sqlite_master WHERE type='table' AND name='audit_log'")
                assert cursor.fetchone() is not None
            """,
            "");

        var result = await runner.RunAsync(
            new Dictionary<string, string>
            {
                ["models.py"] = """
                from peewee import AutoField, Model, SqliteDatabase

                db = SqliteDatabase(":memory:")

                class Todo(Model):
                    id = AutoField()

                    class Meta:
                        database = db

                class AuditLog(Model):
                    audit_id = AutoField()

                    class Meta:
                        database = db
                """,
                ["migration.py"] = """
                from models import AuditLog, db

                def run_migration():
                    db.connect(reuse_if_open=True)
                    db.create_tables([AuditLog], safe=True)
                """
            },
            "python",
            testCase,
            CancellationToken.None);

        Assert.True(result.ExitCode == 0, result.Stderr ?? result.Stdout);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task JavaScript_successful_execution_returns_zero_exit_code()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var runner = new DockerCodeRunner();
        var testCase = CreateTestCase(
            "",
            "const { solve } = require('./solution.js');\ntest('adds 3 + 4', () => {\n    expect(solve(3, 4)).toBe(7);\n});\n"
        );

        var result = await runner.RunAsync(
            new Dictionary<string, string> { ["solution.js"] = "function solve(a, b) {\n    return a + b;\n}\nmodule.exports = { solve };\n" },
            "javascript",
            testCase,
            CancellationToken.None
        );

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Null(result.Stderr);
    }

    [Fact]
    public async Task JavaScript_dom_tests_run_with_jsdom_environment()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var runner = new DockerCodeRunner();
        var testCase = CreateTestCase(
            "",
            """
            test('button clears all tasks', () => {
              document.body.innerHTML = `
                <input id="taskInput" />
                <button id="addBtn">Add Task</button>
                <button id="clearBtn">Clear All</button>
                <ul id="taskList"><li>First</li><li>Second</li></ul>
              `;
              require('./app.js');
              document.getElementById('clearBtn').click();
              expect(document.querySelectorAll('#taskList li')).toHaveLength(0);
            });
            """
        );

        var result = await runner.RunAsync(
            new Dictionary<string, string>
            {
                ["app.js"] = """
                const taskList = document.getElementById('taskList');
                const clearBtn = document.getElementById('clearBtn');
                clearBtn.addEventListener('click', () => {
                  taskList.innerHTML = '';
                });
                """
            },
            "javascript",
            testCase,
            CancellationToken.None
        );

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Null(result.Stderr);
    }

    [Fact]
    public async Task JavaScript_tests_can_import_jsdom_directly()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var runner = new DockerCodeRunner();
        var testCase = CreateTestCase(
            "",
            """
            const { JSDOM } = require('jsdom');

            test('direct jsdom import works', () => {
              const dom = new JSDOM('<button>Clear All</button>');
              expect(dom.window.document.querySelector('button').textContent).toBe('Clear All');
            });
            """
        );

        var result = await runner.RunAsync(
            new Dictionary<string, string> { ["app.js"] = "" },
            "javascript",
            testCase,
            CancellationToken.None
        );

        Assert.True(result.ExitCode == 0, result.Stderr ?? result.Stdout);
        Assert.False(result.TimedOut);
        Assert.Null(result.Stderr);
    }

    [Fact]
    public async Task Html_workspace_harness_provides_dom_and_websocket_globals()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var runner = new DockerCodeRunner();
        var testCase = CreateTestCase(
            "",
            """
            require('./app.js');

            test('generated browser dependencies are available', () => {
              ws.send({ type: 'insert', position: 0, text: 'A' });
              document.getElementById('doc').value = 'Hello';
              document.getElementById('doc').dispatchEvent(new Event('input'));

              expect(ws.sent).toHaveLength(1);
              expect(JSON.parse(localStorage.getItem('docState')).content).toBe('Hello');
            });
            """
        );

        var result = await runner.RunAsync(
            new Dictionary<string, string>
            {
                ["index.html"] = "<!doctype html><html><body><textarea id=\"doc\"></textarea><script src=\"app.js\"></script></body></html>",
                ["app.js"] = """
                const doc = document.getElementById('doc');
                doc.addEventListener('input', () => {
                  localStorage.setItem('docState', JSON.stringify({ content: doc.value }));
                });
                """
            },
            "html",
            testCase,
            CancellationToken.None
        );

        Assert.True(result.ExitCode == 0, result.Stderr ?? result.Stdout);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task Html_workspace_harness_uses_loaded_page_when_generated_test_creates_empty_jsdom()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var runner = new DockerCodeRunner();
        var testCase = CreateTestCase(
            "",
            """
            const { JSDOM } = require('jsdom');
            const { window } = new JSDOM('<!DOCTYPE html><html><body><div id="app"></div></body></html>', { url: 'http://localhost' });
            const { document, navigator } = window;
            global.fetch = jest.fn();
            global.document = document;
            global.window = window;
            global.navigator = navigator;
            require('./app.js');
            expect(document.querySelector('input[type="checkbox"]')).not.toBeNull();
            """
        );

        var result = await runner.RunAsync(
            new Dictionary<string, string>
            {
                ["index.html"] = "<!doctype html><html><body><ul id=\"todo-list\"></ul><script src=\"app.js\"></script></body></html>",
                ["app.js"] = """
                const list = document.getElementById('todo-list');
                const item = document.createElement('li');
                item.className = 'todo-item';
                item.innerHTML = '<input type="checkbox">';
                list.append(item);
                """
            },
            "html",
            testCase,
            CancellationToken.None);

        Assert.True(result.ExitCode == 0, result.Stderr ?? result.Stdout);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task Sql_runner_executes_raw_sql_test_cases_against_solution_schema()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var runner = new DockerCodeRunner();
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            Name = "RawSql",
            Visibility = TestCaseVisibilities.Public,
            TestCodeJson = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["sql"] = """
                INSERT INTO todos (title, description, completed) VALUES ('Test', 'Desc', 0);
                UPDATE todos SET completed=1 WHERE title='Test';
                SELECT COUNT(*) FROM audit_log WHERE todo_id = (SELECT id FROM todos WHERE title='Test');
                """
            })
        };

        var result = await runner.RunAsync(
            new Dictionary<string, string>
            {
                ["schema.sql"] = """
                CREATE TABLE IF NOT EXISTS todos (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  title TEXT NOT NULL,
                  description TEXT NOT NULL DEFAULT '',
                  completed INTEGER NOT NULL DEFAULT 0 CHECK (completed IN (0, 1))
                );
                """,
                ["seed.sql"] = "",
                ["solution.sql"] = """
                CREATE TABLE IF NOT EXISTS audit_log (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  todo_id INTEGER NOT NULL,
                  operation TEXT NOT NULL CHECK (operation IN ('INSERT', 'UPDATE', 'DELETE')),
                  timestamp TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%S','now')),
                  title TEXT,
                  description TEXT,
                  completed INTEGER,
                  FOREIGN KEY (todo_id) REFERENCES todos(id) ON DELETE CASCADE,
                  UNIQUE(todo_id, operation, timestamp)
                );

                CREATE TRIGGER IF NOT EXISTS audit_insert AFTER INSERT ON todos
                BEGIN
                  INSERT INTO audit_log (todo_id, operation, timestamp, title, description, completed)
                  VALUES (NEW.id, 'INSERT', strftime('%Y-%m-%dT%H:%M:%S','now'), NEW.title, NEW.description, NEW.completed);
                END;

                CREATE TRIGGER IF NOT EXISTS audit_update AFTER UPDATE ON todos
                BEGIN
                  INSERT INTO audit_log (todo_id, operation, timestamp, title, description, completed)
                  VALUES (NEW.id, 'UPDATE', strftime('%Y-%m-%dT%H:%M:%S','now'), NEW.title, NEW.description, NEW.completed);
                END;
                """
            },
            "sql",
            testCase,
            CancellationToken.None);

        Assert.True(result.ExitCode == 0, result.Stderr ?? result.Stdout);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task Snake_case_python_starter_file_can_satisfy_legacy_pascal_case_import()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var runner = new DockerCodeRunner();
        var testCase = CreateTestCase(
            "from TodoSummaryPanel import build_summary\n\ndef test_summary():\n    assert build_summary([{'completed': True}, {'completed': False}])['pending'] == 1\n",
            ""
        );

        var result = await runner.RunAsync(
            new Dictionary<string, string>
            {
                ["todo_summary_panel.py"] = """
                def build_summary(todos):
                    total = len(todos)
                    completed = sum(1 for todo in todos if todo.get("completed") is True)
                    return {"total": total, "completed": completed, "pending": total - completed, "message": ""}
                """
            },
            "python",
            testCase,
            CancellationToken.None
        );

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Null(result.Stderr);
    }

    [Fact]
    public async Task Snake_case_javascript_starter_file_can_satisfy_legacy_pascal_case_require()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var runner = new DockerCodeRunner();
        var testCase = CreateTestCase(
            "",
            "const { buildSummary } = require('./TodoSummaryPanel');\ntest('summary', () => {\n  expect(buildSummary([{ completed: true }, { completed: false }]).pending).toBe(1);\n});\n"
        );

        var result = await runner.RunAsync(
            new Dictionary<string, string>
            {
                ["todo_summary_panel.js"] = """
                function buildSummary(todos) {
                  const total = todos.length;
                  const completed = todos.filter((todo) => todo.completed === true).length;
                  return { total, completed, pending: total - completed, message: "" };
                }
                module.exports = { buildSummary };
                """
            },
            "javascript",
            testCase,
            CancellationToken.None
        );

        Assert.Equal(0, result.ExitCode);
        Assert.False(result.TimedOut);
        Assert.Null(result.Stderr);
    }

    [Fact]
    public async Task Python_infinite_loop_is_terminated_by_timeout()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var runner = new DockerCodeRunner();
        var testCase = CreateTestCase(
            "from solution import solve\ndef test_solve():\n    solve()\n",
            ""
        );

        var result = await runner.RunAsync(
            new Dictionary<string, string> { ["solution.py"] = "def solve():\n    while True:\n        pass\n" },
            "python",
            testCase,
            CancellationToken.None
        );

        // The timeout in GraderCommandFactory should still terminate runaway code.
        Assert.True(result.TimedOut || result.ExitCode != 0);
        Assert.NotNull(result.Stderr);
    }

    [Fact]
    public async Task Network_access_is_blocked_inside_sandbox()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var runner = new DockerCodeRunner();
        
        // Code tries to resolve or connect to external resources
        var testCase = CreateTestCase(
            "from solution import solve\ndef test_solve():\n    assert solve() == 'failed'\n",
            ""
        );

        var result = await runner.RunAsync(
            new Dictionary<string, string> { ["solution.py"] = "import urllib.request\ndef solve():\n    try:\n        urllib.request.urlopen('http://example.com', timeout=1)\n        return 'success'\n    except Exception:\n        return 'failed'\n" },
            "python",
            testCase,
            CancellationToken.None
        );

        // Even if python code runs, the connection should fail immediately
        Assert.Equal(0, result.ExitCode); // The test asserts it failed successfully
        Assert.False(result.TimedOut);
        Assert.Contains("passed", result.Stdout);
    }

    [Fact]
    public async Task Python_syntax_error_is_captured_in_stderr()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var runner = new DockerCodeRunner();
        var testCase = CreateTestCase(
            "from solution import solve\ndef test_solve():\n    solve(3, 4)\n",
            ""
        );

        var result = await runner.RunAsync(
            new Dictionary<string, string> { ["solution.py"] = "def solve(a, b)\n    return a + b\n" }, // missing colon
            "python",
            testCase,
            CancellationToken.None
        );

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotNull(result.Stderr);
        Assert.Contains("SyntaxError", result.Stderr);
    }

    [Fact]
    public async Task RunDatabaseSeeding()
    {
        if (!await IsSeedDatabaseAvailable())
        {
            return;
        }

        var optionsBuilder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Backend.Persistence.OjSharpDbContext>();
        optionsBuilder.UseNpgsql(SeedDatabaseConnectionString);
        using var dbContext = new Backend.Persistence.OjSharpDbContext(optionsBuilder.Options);
        await new Backend.Persistence.SchemaCompatibilityService(dbContext).EnsureAsync(CancellationToken.None);

        var PythonAssessmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var FibonacciQuestionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        var existingAssessment = await dbContext.Assessments.Include(a => a.Questions)
            .FirstOrDefaultAsync(a => a.Id == PythonAssessmentId);

        Assert.NotNull(existingAssessment);

        if (!existingAssessment.Questions.Any(q => q.Id == FibonacciQuestionId))
        {
            var now = DateTimeOffset.UtcNow;
            var fibQuestion = new Question
            {
                Id = FibonacciQuestionId,
                AssessmentId = PythonAssessmentId,
                Title = "Fibonacci Number",
                ProblemDescriptionMarkdown = "## Task\nWrite a function that returns the n-th Fibonacci number.\n\nThe sequence is defined as:\n- F(0) = 0\n- F(1) = 1\n- F(n) = F(n-1) + F(n-2) for n > 1.\n\n### Examples\n- `n = 5` -> returns `5`\n- `n = 8` -> returns `21`",
                LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "python", "javascript", "typescript" }),
                StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                {
                    ["python"] = "def solve(n):\n    # TODO: return the n-th Fibonacci number\n    pass\n",
                    ["javascript"] = "function solve(n) {\n  // TODO: return the n-th Fibonacci number\n}\n",
                    ["typescript"] = "function solve(n: number): number {\n  // TODO: return the n-th Fibonacci number\n  return 0;\n}\n"
                }),
                SortOrder = 3,
                MaxScore = 50
            };

            var pythonCode = "from solution import solve\n\n\ndef test_fib_5():\n    assert solve(5) == 5\n";
            var jsCode = "const { solve } = require(\"./solution.js\");\n\ntest(\"fib 5\", () => {\n  expect(solve(5)).toBe(5);\n});\n";
            var tsCode = "const solve = globalThis.__ojsharpSolve;\n\ntest(\"fib 5\", () => {\n  expect(solve(5)).toBe(5);\n});\n";

            var publicTestCase = new TestCase
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                QuestionId = FibonacciQuestionId,
                Name = "sample test 1",
                Visibility = TestCaseVisibilities.Public,
                TestCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                {
                    ["python"] = pythonCode,
                    ["javascript"] = jsCode,
                    ["typescript"] = tsCode
                })
            };

            var pythonCodeHidden = "from solution import solve\n\n\ndef test_fib_8():\n    assert solve(8) == 21\n";
            var jsCodeHidden = "const { solve } = require(\"./solution.js\");\n\ntest(\"fib 8\", () => {\n  expect(solve(8)).toBe(21);\n});\n";
            var tsCodeHidden = "const solve = globalThis.__ojsharpSolve;\n\ntest(\"fib 8\", () => {\n  expect(solve(8)).toBe(21);\n});\n";

            var hiddenTestCase = new TestCase
            {
                Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                QuestionId = FibonacciQuestionId,
                Name = "hidden large number",
                Visibility = TestCaseVisibilities.Hidden,
                TestCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                {
                    ["python"] = pythonCodeHidden,
                    ["javascript"] = jsCodeHidden,
                    ["typescript"] = tsCodeHidden
                })
            };

            fibQuestion.TestCases.Add(publicTestCase);
            fibQuestion.TestCases.Add(hiddenTestCase);

            dbContext.Questions.Add(fibQuestion);
            await dbContext.SaveChangesAsync();
        }
    }
}
