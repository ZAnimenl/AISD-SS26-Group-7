using Backend.Configuration;
using Backend.Domain;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Persistence;

public sealed class DemoDataSeeder(
    OjSharpDbContext dbContext,
    PasswordHasher passwordHasher,
    IOptions<SeedAdminOptions> seedAdminOptions)
{
    public static readonly Guid StudentUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid AdminUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid WebDevAssessmentId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid UiTaskId = Guid.Parse("44444444-4444-4444-4444-444444444401");
    public static readonly Guid ApiTaskId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid DatabaseTaskId = Guid.Parse("44444444-4444-4444-4444-444444444403");
    public static readonly Guid BugFixTaskId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        seedAdminOptions.Value.Validate();

        var now = DateTimeOffset.UtcNow;
        await EnsureSeedAdminAsync(now, cancellationToken);
        await EnsureDemoStudentAsync(now, cancellationToken);
        await UpsertAssessmentAsync(now, cancellationToken);
    }

    private async Task EnsureSeedAdminAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var options = seedAdminOptions.Value;
        var admin = await dbContext.Users.FirstOrDefaultAsync(user => user.Email == options.Email, cancellationToken);
        if (admin is null)
        {
            dbContext.Users.Add(new User
            {
                Id = AdminUserId,
                FullName = "Ada Admin",
                Email = options.Email,
                PasswordHash = passwordHasher.Hash(options.Password),
                Role = UserRoles.Administrator,
                Status = UserStatuses.Active,
                CreatedAt = now
            });
            return;
        }

        admin.FullName = string.IsNullOrWhiteSpace(admin.FullName) ? "Ada Admin" : admin.FullName;
        admin.PasswordHash = passwordHasher.Hash(options.Password);
        admin.Role = UserRoles.Administrator;
        admin.Status = UserStatuses.Active;
    }

    private async Task EnsureDemoStudentAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await dbContext.Users.AnyAsync(user => user.Id == StudentUserId || user.Email == "student@example.com", cancellationToken))
        {
            return;
        }

        dbContext.Users.Add(new User
        {
            Id = StudentUserId,
            FullName = "Alice Student",
            Email = "student@example.com",
            PasswordHash = passwordHasher.Hash("password"),
            Role = UserRoles.Student,
            Status = UserStatuses.Active,
            CreatedAt = now
        });
    }

    private async Task UpsertAssessmentAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var assessment = await dbContext.Assessments
            .Include(item => item.Questions)
            .ThenInclude(question => question.TestCases)
            .FirstOrDefaultAsync(item => item.Id == WebDevAssessmentId, cancellationToken);

        if (assessment is null)
        {
            assessment = new Assessment
            {
                Id = WebDevAssessmentId,
                CreatedAt = now
            };
            dbContext.Assessments.Add(assessment);
        }

        assessment.Title = "Todo App AI Coding Assessment";
        assessment.Description = "Complete four focused Todo app development tasks based on a shared prototype. Work only in the browser workspace; the platform supplies starter files, preview metadata, and automated grading.";
        assessment.DurationMinutes = 75;
        assessment.Status = AssessmentStatuses.Active;
        assessment.AiEnabled = true;
        assessment.ArchivedAt = null;
        assessment.SharedPrototypeReference = "todo-app";
        assessment.SharedPrototypeVersion = "seed-v1";
        assessment.SharedPrototypeMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
        {
            ["display_name"] = "Todo App",
            ["source_prototype"] = "vite-python-fastapi-todolist",
            ["student_setup"] = "platform_native",
            ["dependency_install_required"] = "false"
        });

        var expectedQuestionIds = new HashSet<Guid>
        {
            UiTaskId,
            ApiTaskId,
            DatabaseTaskId,
            BugFixTaskId
        };
        var shouldReplaceQuestions = assessment.Questions.Count != expectedQuestionIds.Count
            || assessment.Questions.Any(question => !expectedQuestionIds.Contains(question.Id))
            || assessment.Questions.Any(question => question.StarterPrototypeReference != "todo-app");

        if (shouldReplaceQuestions)
        {
            var previousQuestions = assessment.Questions.ToList();
            dbContext.TestCases.RemoveRange(previousQuestions.SelectMany(question => question.TestCases));
            dbContext.Questions.RemoveRange(previousQuestions);
            assessment.Questions.Clear();
            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var question in previousQuestions)
            {
                foreach (var testCase in question.TestCases)
                {
                    dbContext.Entry(testCase).State = EntityState.Detached;
                }

                dbContext.Entry(question).State = EntityState.Detached;
            }

            dbContext.Questions.AddRange(
                CreateTodoUiExtensionTask(),
                CreateTodoApiTask(),
                CreateTodoDatabaseTask(),
                CreateTodoBugFixTask());
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Question CreateTodoUiExtensionTask()
    {
        return new Question
        {
            Id = UiTaskId,
            AssessmentId = WebDevAssessmentId,
            Title = "Add a Todo Summary Panel",
            TaskType = TaskTypes.FrontendUiExtension,
            Difficulty = "easy",
            VerificationMode = VerificationModes.BrowserUiPreview,
            StarterPrototypeReference = "todo-app",
            StarterFilesMetadataJson = JsonDocumentSerializer.Serialize(TaskStarterMetadata("TodoSummaryPanel.py", "TodoSummaryPanel.js")),
            VerificationMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["primary_view"] = VerificationModes.BrowserUiPreview,
                ["preview_entry"] = "TodoSummaryPanel.js",
                ["preview_kind"] = "html_component"
            }),
            GradingConfigurationJson = JsonDocumentSerializer.Serialize(AutomatedGradingConfiguration()),
            TraceabilityMetadataJson = JsonDocumentSerializer.Serialize(Traceability("REQ-17,REQ-18c,REQ-18d,REQ-18e,REQ-30c")),
            ProblemDescriptionMarkdown = """
## Task: Add a Todo Summary Panel

The Todo prototype already has a list, create form, and completion checkboxes. The workspace contains a small platform-native component extracted from that UI.

**Your task:** implement the summary panel so the preview can show the current Todo progress.

### Requirements

1. Show the heading `Todo Summary`.
2. Show the total number of todos.
3. Show the number of completed and pending todos.
4. Include the message `All tasks complete` only when every todo is completed and the list is not empty.

### Files

| File | Purpose |
|---|---|
| `TodoSummaryPanel.py` / `TodoSummaryPanel.js` | Editable component logic used by the preview |

The platform provides the browser preview and grading runtime. You do not need to install or run Vite, React, or any local server.
""",
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(RequiredLanguages()),
            StarterCodeJson = JsonDocumentSerializer.Serialize(StarterFiles(
                python: new Dictionary<string, string>
                {
                    ["TodoSummaryPanel.py"] = """
def build_summary(todos):
    completed = 0
    pending = 0

    return {
        "total": 0,
        "completed": completed,
        "pending": pending,
        "message": ""
    }

def render_summary_panel(todos):
    summary = build_summary(todos)
    return (
        '<section data-testid="todo-summary">'
        '<h2>Todo Summary</h2>'
        f'<p>Total: {summary["total"]}</p>'
        f'<p>Completed: {summary["completed"]}</p>'
        f'<p>Pending: {summary["pending"]}</p>'
        f'<p>{summary["message"]}</p>'
        '</section>'
    )
"""
                },
                javascript: new Dictionary<string, string>
                {
                    ["TodoSummaryPanel.js"] = """
function buildSummary(todos) {
  const completed = 0;
  const pending = 0;

  return {
    total: 0,
    completed,
    pending,
    message: "",
  };
}

function renderSummaryPanel(todos) {
  const summary = buildSummary(todos);
  return [
    '<section data-testid="todo-summary">',
    '<h2>Todo Summary</h2>',
    `<p>Total: ${summary.total}</p>`,
    `<p>Completed: ${summary.completed}</p>`,
    `<p>Pending: ${summary.pending}</p>`,
    `<p>${summary.message}</p>`,
    '</section>',
  ].join('');
}

module.exports = { buildSummary, renderSummaryPanel };
"""
                })),
            SortOrder = 1,
            MaxScore = 25,
            TestCases =
            [
                TestCase("Summary counts visible todos", TestCaseVisibilities.Public,
                    python: """
from TodoSummaryPanel import build_summary, render_summary_panel

def test_summary_counts_visible_todos():
    todos = [
        {"title": "Buy groceries", "completed": False},
        {"title": "Write tests", "completed": True},
        {"title": "Review PR", "completed": False},
    ]
    summary = build_summary(todos)
    assert summary["total"] == 3
    assert summary["completed"] == 1
    assert summary["pending"] == 2
    html = render_summary_panel(todos)
    assert "Todo Summary" in html
    assert "Total: 3" in html
""",
                    javascript: """
const { buildSummary, renderSummaryPanel } = require('./TodoSummaryPanel');

test('summary counts visible todos', () => {
  const todos = [
    { title: 'Buy groceries', completed: false },
    { title: 'Write tests', completed: true },
    { title: 'Review PR', completed: false },
  ];
  const summary = buildSummary(todos);
  expect(summary.total).toBe(3);
  expect(summary.completed).toBe(1);
  expect(summary.pending).toBe(2);
  expect(renderSummaryPanel(todos)).toContain('Total: 3');
});
"""),
                TestCase("Completed list message", TestCaseVisibilities.Hidden,
                    python: """
from TodoSummaryPanel import build_summary, render_summary_panel

def test_completed_list_message():
    todos = [
        {"title": "Ship assessment", "completed": True},
        {"title": "Archive notes", "completed": True},
    ]
    summary = build_summary(todos)
    assert summary["pending"] == 0
    assert summary["message"] == "All tasks complete"
    assert "All tasks complete" in render_summary_panel(todos)
""",
                    javascript: """
const { buildSummary, renderSummaryPanel } = require('./TodoSummaryPanel');

test('completed list message', () => {
  const todos = [
    { title: 'Ship assessment', completed: true },
    { title: 'Archive notes', completed: true },
  ];
  const summary = buildSummary(todos);
  expect(summary.pending).toBe(0);
  expect(summary.message).toBe('All tasks complete');
  expect(renderSummaryPanel(todos)).toContain('All tasks complete');
});
""")
            ]
        };
    }

    private Question CreateTodoApiTask()
    {
        return new Question
        {
            Id = ApiTaskId,
            AssessmentId = WebDevAssessmentId,
            Title = "Implement Todo API Route Handling",
            TaskType = TaskTypes.RestApiDevelopment,
            Difficulty = "medium",
            VerificationMode = VerificationModes.ApiResponseCheck,
            StarterPrototypeReference = "todo-app",
            StarterFilesMetadataJson = JsonDocumentSerializer.Serialize(TaskStarterMetadata("api.py", "api.js")),
            VerificationMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["primary_view"] = VerificationModes.ApiResponseCheck,
                ["endpoint"] = "/api/todos"
            }),
            GradingConfigurationJson = JsonDocumentSerializer.Serialize(AutomatedGradingConfiguration()),
            TraceabilityMetadataJson = JsonDocumentSerializer.Serialize(Traceability("REQ-17,REQ-18c,REQ-18d,REQ-30d")),
            ProblemDescriptionMarkdown = """
## Task: Implement Todo API Route Handling

The source prototype exposes `/api/todos` endpoints through FastAPI. In this assessment, the platform gives you a lightweight route handler with the same behavior, so no web framework is required.

**Your task:** implement request handling in `api.py` / `api.js`.

### Requirements

1. `GET /api/todos` returns status `200` and all todos.
2. `POST /api/todos` accepts a title and optional description, creates an incomplete todo, and returns status `201`.
3. `GET /api/todos/<id>` returns status `200` for an existing todo.
4. Missing todos must return status `404`.

The handler returns dictionaries/objects shaped like `{ status, body }`.
""",
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(RequiredLanguages()),
            StarterCodeJson = JsonDocumentSerializer.Serialize(StarterFiles(
                python: new Dictionary<string, string>
                {
                    ["store.py"] = """
def seed_todos():
    return [
        {"id": 1, "title": "Buy groceries", "description": "Milk and bread", "completed": False},
        {"id": 2, "title": "Write tests", "description": "Todo API coverage", "completed": True},
    ]
""",
                    ["api.py"] = """
from store import seed_todos

todos = seed_todos()

def route_todo_request(method, path, body=None):
    body = body or {}

    if method == "GET" and path == "/api/todos":
        return {"status": 501, "body": {"error": "not implemented"}}

    if method == "POST" and path == "/api/todos":
        return {"status": 501, "body": {"error": "not implemented"}}

    if method == "GET" and path.startswith("/api/todos/"):
        return {"status": 501, "body": {"error": "not implemented"}}

    return {"status": 404, "body": {"error": "not found"}}
"""
                },
                javascript: new Dictionary<string, string>
                {
                    ["store.js"] = """
function seedTodos() {
  return [
    { id: 1, title: 'Buy groceries', description: 'Milk and bread', completed: false },
    { id: 2, title: 'Write tests', description: 'Todo API coverage', completed: true },
  ];
}

module.exports = { seedTodos };
""",
                    ["api.js"] = """
const { seedTodos } = require('./store');

const todos = seedTodos();

function routeTodoRequest(method, path, body = {}) {
  if (method === 'GET' && path === '/api/todos') {
    return { status: 501, body: { error: 'not implemented' } };
  }

  if (method === 'POST' && path === '/api/todos') {
    return { status: 501, body: { error: 'not implemented' } };
  }

  if (method === 'GET' && path.startsWith('/api/todos/')) {
    return { status: 501, body: { error: 'not implemented' } };
  }

  return { status: 404, body: { error: 'not found' } };
}

module.exports = { routeTodoRequest, todos };
"""
                })),
            SortOrder = 2,
            MaxScore = 25,
            TestCases =
            [
                TestCase("GET /api/todos returns todo list", TestCaseVisibilities.Public,
                    python: """
import api

def test_get_todos_returns_list():
    response = api.route_todo_request("GET", "/api/todos")
    assert response["status"] == 200
    assert isinstance(response["body"], list)
    assert len(response["body"]) >= 2
""",
                    javascript: """
const { routeTodoRequest } = require('./api');

test('GET /api/todos returns todo list', () => {
  const response = routeTodoRequest('GET', '/api/todos');
  expect(response.status).toBe(200);
  expect(Array.isArray(response.body)).toBe(true);
  expect(response.body.length).toBeGreaterThanOrEqual(2);
});
"""),
                TestCase("POST /api/todos creates todo", TestCaseVisibilities.Hidden,
                    python: """
import api

def test_post_todos_creates_todo():
    response = api.route_todo_request("POST", "/api/todos", {"title": "Plan sprint"})
    assert response["status"] == 201
    assert response["body"]["title"] == "Plan sprint"
    assert response["body"]["completed"] is False
    assert isinstance(response["body"]["id"], int)
""",
                    javascript: """
const { routeTodoRequest } = require('./api');

test('POST /api/todos creates todo', () => {
  const response = routeTodoRequest('POST', '/api/todos', { title: 'Plan sprint' });
  expect(response.status).toBe(201);
  expect(response.body.title).toBe('Plan sprint');
  expect(response.body.completed).toBe(false);
  expect(Number.isInteger(response.body.id)).toBe(true);
});
"""),
                TestCase("Missing todo returns 404", TestCaseVisibilities.Hidden,
                    python: """
import api

def test_missing_todo_returns_404():
    response = api.route_todo_request("GET", "/api/todos/9999")
    assert response["status"] == 404
""",
                    javascript: """
const { routeTodoRequest } = require('./api');

test('missing todo returns 404', () => {
  const response = routeTodoRequest('GET', '/api/todos/9999');
  expect(response.status).toBe(404);
});
""")
            ]
        };
    }

    private Question CreateTodoDatabaseTask()
    {
        return new Question
        {
            Id = DatabaseTaskId,
            AssessmentId = WebDevAssessmentId,
            Title = "Add Priority Query Support",
            TaskType = TaskTypes.DatabaseQuerySchema,
            Difficulty = "medium",
            VerificationMode = VerificationModes.DatabaseResultCheck,
            StarterPrototypeReference = "todo-app",
            StarterFilesMetadataJson = JsonDocumentSerializer.Serialize(TaskStarterMetadata("repository.py", "repository.js")),
            VerificationMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["primary_view"] = VerificationModes.DatabaseResultCheck,
                ["result_shape"] = "todo_rows"
            }),
            GradingConfigurationJson = JsonDocumentSerializer.Serialize(AutomatedGradingConfiguration()),
            TraceabilityMetadataJson = JsonDocumentSerializer.Serialize(Traceability("REQ-17,REQ-18c,REQ-18d,REQ-30e")),
            ProblemDescriptionMarkdown = """
## Task: Add Priority Query Support

The source Todo backend stores todos through a repository. Product feedback asks for a way to surface the next important active todos.

**Your task:** update the lightweight model/repository files to support priority-aware querying.

### Requirements

1. Todos support a `priority` field with values `low`, `normal`, or `high`.
2. New todos default to `normal` priority.
3. `find_next_actionable(limit)` returns incomplete todos only.
4. Results are ordered by priority first (`high`, then `normal`, then `low`) and then by `id`.
5. The method respects the supplied limit.

No local database is required. The platform uses in-memory rows to verify the query behavior.
""",
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(RequiredLanguages()),
            StarterCodeJson = JsonDocumentSerializer.Serialize(StarterFiles(
                python: new Dictionary<string, string>
                {
                    ["model.py"] = """
def make_todo(todo_id, title, completed=False, priority=None):
    return {
        "id": todo_id,
        "title": title,
        "completed": completed,
    }
""",
                    ["repository.py"] = """
from model import make_todo

todos = [
    make_todo(1, "Buy groceries", completed=False),
    make_todo(2, "Write tests", completed=True),
    make_todo(3, "Fix deploy", completed=False),
]

def create_todo(title, priority=None):
    next_id = max(todo["id"] for todo in todos) + 1 if todos else 1
    todo = make_todo(next_id, title, completed=False, priority=priority)
    todos.append(todo)
    return todo

def find_next_actionable(limit=3):
    return todos[:limit]
"""
                },
                javascript: new Dictionary<string, string>
                {
                    ["model.js"] = """
function makeTodo(id, title, completed = false, priority = null) {
  return {
    id,
    title,
    completed,
  };
}

module.exports = { makeTodo };
""",
                    ["repository.js"] = """
const { makeTodo } = require('./model');

const todos = [
  makeTodo(1, 'Buy groceries', false),
  makeTodo(2, 'Write tests', true),
  makeTodo(3, 'Fix deploy', false),
];

function createTodo(title, priority = null) {
  const nextId = todos.length > 0 ? Math.max(...todos.map((todo) => todo.id)) + 1 : 1;
  const todo = makeTodo(nextId, title, false, priority);
  todos.push(todo);
  return todo;
}

function findNextActionable(limit = 3) {
  return todos.slice(0, limit);
}

module.exports = { todos, createTodo, findNextActionable };
"""
                })),
            SortOrder = 3,
            MaxScore = 25,
            TestCases =
            [
                TestCase("New todos default to normal priority", TestCaseVisibilities.Public,
                    python: """
from repository import create_todo

def test_new_todos_default_to_normal_priority():
    todo = create_todo("Document seed task")
    assert todo["priority"] == "normal"
""",
                    javascript: """
const { createTodo } = require('./repository');

test('new todos default to normal priority', () => {
  const todo = createTodo('Document seed task');
  expect(todo.priority).toBe('normal');
});
"""),
                TestCase("Actionable query orders by priority", TestCaseVisibilities.Hidden,
                    python: """
import repository

def test_actionable_query_orders_by_priority():
    repository.todos.clear()
    repository.todos.extend([
        {"id": 1, "title": "Low", "completed": False, "priority": "low"},
        {"id": 2, "title": "Done high", "completed": True, "priority": "high"},
        {"id": 3, "title": "High", "completed": False, "priority": "high"},
        {"id": 4, "title": "Normal", "completed": False, "priority": "normal"},
    ])
    result = repository.find_next_actionable(3)
    assert [todo["id"] for todo in result] == [3, 4, 1]
""",
                    javascript: """
const repository = require('./repository');

test('actionable query orders by priority', () => {
  repository.todos.splice(0, repository.todos.length,
    { id: 1, title: 'Low', completed: false, priority: 'low' },
    { id: 2, title: 'Done high', completed: true, priority: 'high' },
    { id: 3, title: 'High', completed: false, priority: 'high' },
    { id: 4, title: 'Normal', completed: false, priority: 'normal' },
  );
  const result = repository.findNextActionable(3);
  expect(result.map((todo) => todo.id)).toEqual([3, 4, 1]);
});
""")
            ]
        };
    }

    private Question CreateTodoBugFixTask()
    {
        return new Question
        {
            Id = BugFixTaskId,
            AssessmentId = WebDevAssessmentId,
            Title = "Fix Todo Completion Toggle",
            TaskType = TaskTypes.BugFix,
            Difficulty = "easy",
            VerificationMode = VerificationModes.RegressionTest,
            StarterPrototypeReference = "todo-app",
            StarterFilesMetadataJson = JsonDocumentSerializer.Serialize(TaskStarterMetadata("todo_service.py", "todo_service.js")),
            VerificationMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["primary_view"] = VerificationModes.RegressionTest,
                ["focus"] = "toggle_completion"
            }),
            GradingConfigurationJson = JsonDocumentSerializer.Serialize(AutomatedGradingConfiguration()),
            TraceabilityMetadataJson = JsonDocumentSerializer.Serialize(Traceability("REQ-17,REQ-18c,REQ-18d,REQ-30f")),
            ProblemDescriptionMarkdown = """
## Task: Fix Todo Completion Toggle

The Todo prototype has a completion checkbox. A regression caused the toggle behavior to mark todos completed every time instead of flipping between completed and incomplete.

**Your task:** fix the existing todo service behavior.

### Requirements

1. Toggling an incomplete todo marks it completed.
2. Toggling a completed todo marks it incomplete.
3. The returned todo keeps its original title and description.
4. Missing todo IDs still return `None` / `null`.

Only fix the existing behavior. Do not replace the service with a framework or external package.
""",
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(RequiredLanguages()),
            StarterCodeJson = JsonDocumentSerializer.Serialize(StarterFiles(
                python: new Dictionary<string, string>
                {
                    ["todo_service.py"] = """
todos = [
    {"id": 1, "title": "Buy groceries", "description": "Milk and bread", "completed": False},
    {"id": 2, "title": "Write tests", "description": "Todo API coverage", "completed": True},
]

def find_todo(todo_id):
    return next((todo for todo in todos if todo["id"] == todo_id), None)

def toggle_todo_completion(todo_id):
    todo = find_todo(todo_id)
    if todo is None:
        return None

    # BUG: should flip the current value instead of always setting True.
    todo["completed"] = True
    return todo
"""
                },
                javascript: new Dictionary<string, string>
                {
                    ["todo_service.js"] = """
const todos = [
  { id: 1, title: 'Buy groceries', description: 'Milk and bread', completed: false },
  { id: 2, title: 'Write tests', description: 'Todo API coverage', completed: true },
];

function findTodo(id) {
  return todos.find((todo) => todo.id === id) || null;
}

function toggleTodoCompletion(id) {
  const todo = findTodo(id);
  if (!todo) {
    return null;
  }

  // BUG: should flip the current value instead of always setting true.
  todo.completed = true;
  return todo;
}

module.exports = { todos, findTodo, toggleTodoCompletion };
"""
                })),
            SortOrder = 4,
            MaxScore = 25,
            TestCases =
            [
                TestCase("Toggle marks incomplete todo complete", TestCaseVisibilities.Public,
                    python: """
import todo_service

def test_toggle_marks_incomplete_todo_complete():
    todo_service.todos[0]["completed"] = False
    todo = todo_service.toggle_todo_completion(1)
    assert todo["completed"] is True
    assert todo["title"] == "Buy groceries"
""",
                    javascript: """
const service = require('./todo_service');

test('toggle marks incomplete todo complete', () => {
  service.todos[0].completed = false;
  const todo = service.toggleTodoCompletion(1);
  expect(todo.completed).toBe(true);
  expect(todo.title).toBe('Buy groceries');
});
"""),
                TestCase("Toggle marks completed todo incomplete", TestCaseVisibilities.Hidden,
                    python: """
import todo_service

def test_toggle_marks_completed_todo_incomplete():
    todo_service.todos[1]["completed"] = True
    todo = todo_service.toggle_todo_completion(2)
    assert todo["completed"] is False
    assert todo["description"] == "Todo API coverage"
""",
                    javascript: """
const service = require('./todo_service');

test('toggle marks completed todo incomplete', () => {
  service.todos[1].completed = true;
  const todo = service.toggleTodoCompletion(2);
  expect(todo.completed).toBe(false);
  expect(todo.description).toBe('Todo API coverage');
});
"""),
                TestCase("Missing todo returns null result", TestCaseVisibilities.Hidden,
                    python: """
from todo_service import toggle_todo_completion

def test_missing_todo_returns_none():
    assert toggle_todo_completion(9999) is None
""",
                    javascript: """
const { toggleTodoCompletion } = require('./todo_service');

test('missing todo returns null result', () => {
  expect(toggleTodoCompletion(9999)).toBeNull();
});
""")
            ]
        };
    }

    private static TestCase TestCase(string name, string visibility, string python, string javascript)
    {
        return new TestCase
        {
            Id = Guid.NewGuid(),
            Name = name,
            Visibility = visibility,
            TestCodeJson = JsonDocumentSerializer.Serialize(TestCode(python, javascript)),
            AuthoringSource = AuthoringSources.Manual,
            PublicMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["student_visible"] = visibility == TestCaseVisibilities.Public ? "true" : "false"
            }),
            AdminMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["source"] = "todo-app-seed"
            }),
            TraceabilityMetadataJson = JsonDocumentSerializer.Serialize(Traceability("REQ-15,REQ-52,REQ-53"))
        };
    }

    private static Dictionary<string, Dictionary<string, string>> StarterFiles(
        Dictionary<string, string> python,
        Dictionary<string, string> javascript)
    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["python"] = python,
            ["javascript"] = javascript
        };
    }

    private static Dictionary<string, string> TestCode(string python, string javascript)
    {
        return new Dictionary<string, string>
        {
            ["python"] = python,
            ["javascript"] = javascript
        };
    }

    private static Dictionary<string, Dictionary<string, string>> TaskStarterMetadata(string pythonEditableFile, string javascriptEditableFile)
    {
        return new Dictionary<string, Dictionary<string, string>>
        {
            ["python"] = new Dictionary<string, string>
            {
                [pythonEditableFile] = "editable"
            },
            ["javascript"] = new Dictionary<string, string>
            {
                [javascriptEditableFile] = "editable"
            }
        };
    }

    private static string[] RequiredLanguages()
    {
        return ["python", "javascript"];
    }

    private static Dictionary<string, string> AutomatedGradingConfiguration()
    {
        return new Dictionary<string, string>
        {
            ["runner"] = "automated_tests",
            ["requires_student_install"] = "false"
        };
    }

    private static Dictionary<string, string> Traceability(string requirementIds)
    {
        return new Dictionary<string, string>
        {
            ["requirements"] = requirementIds,
            ["source"] = "manual_seed_from_todo_prototype"
        };
    }
}
