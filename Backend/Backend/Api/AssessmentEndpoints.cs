using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api;

public static class AssessmentEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/admin/assessments", ListAdminAsync);
        api.MapPost("/admin/assessments", CreateAsync);
        api.MapPost("/admin/assessments/generate", GenerateAsync);
        api.MapGet("/admin/assessments/{assessmentId:guid}", GetAdminAsync);
        api.MapPut("/admin/assessments/{assessmentId:guid}", UpdateAsync);
        api.MapPost("/admin/assessments/{assessmentId:guid}/archive", ArchiveAsync);
        api.MapDelete("/admin/assessments/{assessmentId:guid}", DeleteAsync);
        api.MapGet("/assessments/{assessmentId:guid}/context", ContextAsync);
    }

    private static async Task<IResult> ListAdminAsync(
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        AssessmentProjectionService projectionService,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var assessments = await dbContext.Assessments
            .Include(assessment => assessment.Questions)
            .OrderByDescending(assessment => assessment.CreatedAt)
            .ToListAsync(cancellationToken);

        return ApiResults.Success(assessments.Select(projectionService.ToAdminAssessment));
    }

    private static async Task<IResult> CreateAsync(
        AssessmentRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            DurationMinutes = request.DurationMinutes,
            Status = NormalizeAssessmentStatus(request.Status),
            AiEnabled = request.AiEnabled,
            SharedPrototypeReference = NormalizeOptionalText(request.SharedPrototypeReference),
            SharedPrototypeVersion = NormalizeOptionalText(request.SharedPrototypeVersion),
            SharedPrototypeMetadataJson = JsonDocumentSerializer.Serialize(request.SharedPrototypeMetadata ?? new Dictionary<string, string>()),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Assessments.Add(assessment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { assessment_id = assessment.Id });
    }

    private static async Task<IResult> GenerateAsync(
        AssessmentRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            DurationMinutes = request.DurationMinutes,
            Status = NormalizeAssessmentStatus(request.Status),
            AiEnabled = request.AiEnabled,
            SharedPrototypeReference = NormalizeOptionalText(request.SharedPrototypeReference) ?? "todo-app",
            SharedPrototypeVersion = NormalizeOptionalText(request.SharedPrototypeVersion) ?? "seed-v1",
            SharedPrototypeMetadataJson = JsonDocumentSerializer.Serialize(request.SharedPrototypeMetadata ?? DefaultTodoPrototypeMetadata()),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Assessments.Add(assessment);
        assessment.Questions.AddRange(CreateDefaultTodoTasks(assessment.Id, AuthoringSources.LlmGenerated));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { assessment_id = assessment.Id });
    }

    private static async Task<IResult> GetAdminAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        AssessmentProjectionService projectionService,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var assessment = await dbContext.Assessments
            .Include(item => item.Questions.OrderBy(question => question.SortOrder))
            .ThenInclude(question => question.TestCases)
            .FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken);

        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        return ApiResults.Success(projectionService.ToAdminAssessmentDetail(assessment));
    }

    private static async Task<IResult> UpdateAsync(
        Guid assessmentId,
        AssessmentRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var assessment = await dbContext.Assessments.FindAsync([assessmentId], cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        assessment.Title = request.Title;
        assessment.Description = request.Description;
        assessment.DurationMinutes = request.DurationMinutes;
        assessment.Status = NormalizeAssessmentStatus(request.Status);
        assessment.AiEnabled = request.AiEnabled;
        assessment.SharedPrototypeReference = NormalizeOptionalText(request.SharedPrototypeReference);
        assessment.SharedPrototypeVersion = NormalizeOptionalText(request.SharedPrototypeVersion);
        assessment.SharedPrototypeMetadataJson = JsonDocumentSerializer.Serialize(request.SharedPrototypeMetadata ?? new Dictionary<string, string>());
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { assessment_id = assessment.Id });
    }

    private static async Task<IResult> ArchiveAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var assessment = await dbContext.Assessments.FindAsync([assessmentId], cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        assessment.Status = AssessmentStatuses.Archived;
        assessment.ArchivedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { assessment_id = assessment.Id, assessment.Status });
    }

    private static async Task<IResult> DeleteAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var assessment = await dbContext.Assessments.FindAsync([assessmentId], cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        assessment.Status = AssessmentStatuses.Archived;
        assessment.ArchivedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(new { assessment_id = assessment.Id, deleted = false, archived = true });
    }

    private static async Task<IResult> ContextAsync(
        Guid assessmentId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        AssessmentProjectionService projectionService,
        SchemaCompatibilityService schemaCompatibilityService,
        CancellationToken cancellationToken)
    {
        var (user, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Student, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        await schemaCompatibilityService.EnsureAsync(cancellationToken);

        var assessment = await dbContext.Assessments
            .Include(item => item.Questions.OrderBy(question => question.SortOrder))
            .FirstOrDefaultAsync(item => item.Id == assessmentId, cancellationToken);
        if (assessment is null)
        {
            return ApiResults.Error("ASSESSMENT_NOT_FOUND", "Assessment was not found.", StatusCodes.Status404NotFound);
        }

        var session = await dbContext.AssessmentSessions.FirstOrDefaultAsync(
            item => item.AssessmentId == assessmentId
                    && item.UserId == user!.Id
                    && item.Status == SessionStatuses.Active
                    && item.ExpiresAt > DateTimeOffset.UtcNow,
            cancellationToken);
        if (session is null)
        {
            return ApiResults.Error("ATTEMPT_NOT_FOUND", "Active assessment attempt was not found.", StatusCodes.Status404NotFound);
        }

        return ApiResults.Success(projectionService.ToStudentContext(assessment, session));
    }

    private static List<Question> CreateDefaultTodoTasks(Guid assessmentId, string authoringSource)
    {
        return
        [
            CreateTodoTask(
                assessmentId,
                "Add a Todo Summary Panel",
                TaskTypes.FrontendUiExtension,
                "easy",
                VerificationModes.BrowserUiPreview,
                1,
                """
                ## Task: Add a Todo Summary Panel

                The Todo prototype has a list, create form, and completion checkboxes.

                **Your task:** implement the summary panel so the preview can show current Todo progress.

                ### Requirements

                1. Show the heading `Todo Summary`.
                2. Show total, completed, and pending todo counts.
                3. Show `All tasks complete` only when every todo is completed and the list is not empty.

                The platform provides preview and test execution. You do not need to install dependencies or start a local server.
                """,
                StarterFiles(
                    new Dictionary<string, string>
                    {
                        ["TodoSummaryPanel.py"] = """
                        def build_summary(todos):
                            return {"total": 0, "completed": 0, "pending": 0, "message": ""}

                        def render_summary_panel(todos):
                            summary = build_summary(todos)
                            return f"Todo Summary: {summary}"
                        """,
                        ["preview_data.py"] = """
                        sample_todos = [
                            {"title": "Buy groceries", "completed": False},
                            {"title": "Write tests", "completed": True},
                        ]
                        """
                    },
                    new Dictionary<string, string>
                    {
                        ["TodoSummaryPanel.js"] = """
                        function buildSummary(todos) {
                          return { total: 0, completed: 0, pending: 0, message: '' };
                        }

                        function renderSummaryPanel(todos) {
                          const summary = buildSummary(todos);
                          return `Todo Summary: ${JSON.stringify(summary)}`;
                        }

                        module.exports = { buildSummary, renderSummaryPanel };
                        """,
                        ["previewData.js"] = """
                        const sampleTodos = [
                          { title: 'Buy groceries', completed: false },
                          { title: 'Write tests', completed: true },
                        ];

                        module.exports = { sampleTodos };
                        """
                    }),
                "Summary counts visible todos",
                """
                from TodoSummaryPanel import build_summary

                def test_summary_counts_visible_todos():
                    summary = build_summary([
                        {"title": "Buy groceries", "completed": False},
                        {"title": "Write tests", "completed": True},
                    ])
                    assert summary["total"] == 2
                    assert summary["completed"] == 1
                    assert summary["pending"] == 1
                """,
                """
                const { buildSummary } = require('./TodoSummaryPanel');

                test('summary counts visible todos', () => {
                  const summary = buildSummary([
                    { title: 'Buy groceries', completed: false },
                    { title: 'Write tests', completed: true },
                  ]);
                  expect(summary.total).toBe(2);
                  expect(summary.completed).toBe(1);
                  expect(summary.pending).toBe(1);
                });
                """,
                authoringSource),
            CreateTodoTask(
                assessmentId,
                "Implement Todo API Route Handling",
                TaskTypes.RestApiDevelopment,
                "medium",
                VerificationModes.ApiResponseCheck,
                2,
                """
                ## Task: Implement Todo API Route Handling

                The Todo prototype exposes `/api/todos` behavior. The platform gives you a lightweight route handler, so no web framework is required.

                **Your task:** implement request handling in `api.py` / `api.js`.

                ### Requirements

                1. `GET /api/todos` returns status `200` and all todos.
                2. `POST /api/todos` creates an incomplete todo and returns status `201`.
                3. `GET /api/todos/<id>` returns status `200` for an existing todo.
                4. Missing todos return status `404`.
                """,
                StarterFiles(
                    new Dictionary<string, string>
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
                            return {"status": 501, "body": {"error": "not implemented"}}
                        """
                    },
                    new Dictionary<string, string>
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
                          return { status: 501, body: { error: 'not implemented' } };
                        }

                        module.exports = { routeTodoRequest, todos };
                        """
                    }),
                "GET /api/todos returns todo list",
                """
                import api

                def test_get_todos_returns_list():
                    response = api.route_todo_request("GET", "/api/todos")
                    assert response["status"] == 200
                    assert isinstance(response["body"], list)
                """,
                """
                const { routeTodoRequest } = require('./api');

                test('GET /api/todos returns todo list', () => {
                  const response = routeTodoRequest('GET', '/api/todos');
                  expect(response.status).toBe(200);
                  expect(Array.isArray(response.body)).toBe(true);
                });
                """,
                authoringSource),
            CreateTodoTask(
                assessmentId,
                "Add Priority Query Support",
                TaskTypes.DatabaseQuerySchema,
                "medium",
                VerificationModes.DatabaseResultCheck,
                3,
                """
                ## Task: Add Priority Query Support

                The Todo repository needs to surface the next important active todos.

                ### Requirements

                1. Todos support a `priority` field with `low`, `normal`, or `high`.
                2. New todos default to `normal`.
                3. `find_next_actionable(limit)` returns incomplete todos only.
                4. Results are ordered by priority first and then by `id`.
                """,
                StarterFiles(
                    new Dictionary<string, string>
                    {
                        ["model.py"] = """
                        def make_todo(todo_id, title, completed=False, priority=None):
                            return {"id": todo_id, "title": title, "completed": completed}
                        """,
                        ["repository.py"] = """
                        from model import make_todo

                        todos = [
                            make_todo(1, "Buy groceries", completed=False),
                            make_todo(2, "Write tests", completed=True),
                        ]

                        def create_todo(title, priority=None):
                            todo = make_todo(len(todos) + 1, title, False, priority)
                            todos.append(todo)
                            return todo

                        def find_next_actionable(limit=3):
                            return todos[:limit]
                        """
                    },
                    new Dictionary<string, string>
                    {
                        ["model.js"] = """
                        function makeTodo(id, title, completed = false, priority = null) {
                          return { id, title, completed };
                        }

                        module.exports = { makeTodo };
                        """,
                        ["repository.js"] = """
                        const { makeTodo } = require('./model');

                        const todos = [
                          makeTodo(1, 'Buy groceries', false),
                          makeTodo(2, 'Write tests', true),
                        ];

                        function createTodo(title, priority = null) {
                          const todo = makeTodo(todos.length + 1, title, false, priority);
                          todos.push(todo);
                          return todo;
                        }

                        function findNextActionable(limit = 3) {
                          return todos.slice(0, limit);
                        }

                        module.exports = { todos, createTodo, findNextActionable };
                        """
                    }),
                "New todos default to normal priority",
                """
                from repository import create_todo

                def test_new_todos_default_to_normal_priority():
                    todo = create_todo("Document seed task")
                    assert todo["priority"] == "normal"
                """,
                """
                const { createTodo } = require('./repository');

                test('new todos default to normal priority', () => {
                  const todo = createTodo('Document seed task');
                  expect(todo.priority).toBe('normal');
                });
                """,
                authoringSource),
            CreateTodoTask(
                assessmentId,
                "Fix Todo Completion Toggle",
                TaskTypes.BugFix,
                "easy",
                VerificationModes.RegressionTest,
                4,
                """
                ## Task: Fix Todo Completion Toggle

                A regression caused the Todo completion toggle to mark todos completed every time instead of flipping the value.

                ### Requirements

                1. Toggling an incomplete todo marks it completed.
                2. Toggling a completed todo marks it incomplete.
                3. Missing todo IDs still return `None` / `null`.
                """,
                StarterFiles(
                    new Dictionary<string, string>
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
                            todo["completed"] = True
                            return todo
                        """,
                        ["regression_notes.py"] = "expected_behavior = 'toggle completed between true and false'\n"
                    },
                    new Dictionary<string, string>
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
                          if (!todo) return null;
                          todo.completed = true;
                          return todo;
                        }

                        module.exports = { todos, findTodo, toggleTodoCompletion };
                        """,
                        ["regressionNotes.js"] = "module.exports = { expectedBehavior: 'toggle completed between true and false' };\n"
                    }),
                "Toggle marks incomplete todo complete",
                """
                import todo_service

                def test_toggle_marks_incomplete_todo_complete():
                    todo_service.todos[0]["completed"] = False
                    assert todo_service.toggle_todo_completion(1)["completed"] is True
                """,
                """
                const service = require('./todo_service');

                test('toggle marks incomplete todo complete', () => {
                  service.todos[0].completed = false;
                  expect(service.toggleTodoCompletion(1).completed).toBe(true);
                });
                """,
                authoringSource)
        ];
    }

    private static Dictionary<string, string> DefaultTodoPrototypeMetadata()
    {
        return new Dictionary<string, string>
        {
            ["display_name"] = "Todo App",
            ["source_prototype"] = "vite-python-fastapi-todolist",
            ["student_setup"] = "platform_native",
            ["dependency_install_required"] = "false"
        };
    }

    private static Question CreateTodoTask(
        Guid assessmentId,
        string title,
        string taskType,
        string difficulty,
        string verificationMode,
        int sortOrder,
        string description,
        Dictionary<string, Dictionary<string, string>> starterCode,
        string publicTestName,
        string pythonPublicTest,
        string javascriptPublicTest,
        string authoringSource)
    {
        return new Question
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessmentId,
            Title = title,
            TaskType = taskType,
            Difficulty = difficulty,
            VerificationMode = verificationMode,
            StarterPrototypeReference = "todo-app",
            ProblemDescriptionMarkdown = description,
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "python", "javascript" }),
            StarterCodeJson = JsonDocumentSerializer.Serialize(starterCode),
            StarterFilesMetadataJson = JsonDocumentSerializer.Serialize(BuildStarterMetadata(starterCode)),
            VerificationMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string> { ["primary_view"] = verificationMode }),
            GradingConfigurationJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["runner"] = "automated_tests",
                ["requires_student_install"] = "false"
            }),
            AuthoringSource = authoringSource,
            TraceabilityMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["requirements"] = "REQ-17,REQ-18b,REQ-18c,REQ-18d,REQ-30a,REQ-30b",
                ["source"] = "default_todo_prototype_tasks"
            }),
            SortOrder = sortOrder,
            MaxScore = 25,
            TestCases =
            [
                new TestCase
                {
                    Id = Guid.NewGuid(),
                    Name = publicTestName,
                    Visibility = TestCaseVisibilities.Public,
                    TestCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["python"] = pythonPublicTest,
                        ["javascript"] = javascriptPublicTest
                    }),
                    AuthoringSource = authoringSource,
                    PublicMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string> { ["student_visible"] = "true" }),
                    AdminMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>()),
                    TraceabilityMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string> { ["requirements"] = "REQ-15,REQ-52,REQ-53" })
                }
            ]
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

    private static Dictionary<string, Dictionary<string, string>> BuildStarterMetadata(Dictionary<string, Dictionary<string, string>> starterCode)
    {
        return starterCode.ToDictionary(
            language => language.Key,
            language => language.Value.ToDictionary(file => file.Key, _ => "editable"));
    }

    private static string NormalizeTaskType(string? taskType)
    {
        return taskType switch
        {
            TaskTypes.FrontendUiExtension => TaskTypes.FrontendUiExtension,
            TaskTypes.RestApiDevelopment => TaskTypes.RestApiDevelopment,
            TaskTypes.DatabaseQuerySchema => TaskTypes.DatabaseQuerySchema,
            TaskTypes.BugFix => TaskTypes.BugFix,
            TaskTypes.LegacyWebApplication => TaskTypes.FrontendUiExtension,
            TaskTypes.LegacyApiDevelopment => TaskTypes.RestApiDevelopment,
            TaskTypes.LegacyDatabaseTask => TaskTypes.DatabaseQuerySchema,
            _ => TaskTypes.RestApiDevelopment
        };
    }

    private static string NormalizeAssessmentStatus(string status)
    {
        return status is AssessmentStatuses.Draft or AssessmentStatuses.Active or AssessmentStatuses.Closed or AssessmentStatuses.Archived
            ? status
            : AssessmentStatuses.Draft;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
