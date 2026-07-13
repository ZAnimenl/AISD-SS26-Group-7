using Backend.Contracts;
using Backend.Domain;

namespace Backend.Services;

internal static class AssessmentDraftPromptFactory
{
    public static string BuildSystemPrompt(int minimumStarterFilesPerLanguage)
    {
        return string.Join("\n",
        [
            "You generate coding assessment draft tasks for administrator review.",
            "Return only valid JSON. Do not wrap the JSON in Markdown.",
            "The administrator must review every generated task and test before publication.",
            "Do not include provider secrets, hidden grading explanations, or any student-specific data.",
            "Generated tasks must be practical browser-workspace tasks, not algorithm puzzle tasks.",
            "Every task must be a focused extension, backend capability, schema change, or bug fix for the canonical source in assessmentPrototype/. Never invent or regenerate the base application.",
            "The canonical Todo entity has exactly id, title, description, and completed fields. Extensions may add focused fields only when the task explicitly requires the schema/API/UI change.",
            "Preserve the canonical REST routes: GET/POST /api/todos, GET/PUT/DELETE /api/todos/{todo_id}, and POST /api/todos/{todo_id}/toggle.",
            "Preserve the canonical module contracts: browser-safe frontend/index.html, frontend/styles.css, frontend/app.js; Python FastAPI/Peewee backend modules; JavaScript Node/Express server.js, controllers.js, services.js, repositories.js, models.js, schemas.js, and environment.js modules; SQLite or platform-provided file persistence.",
            "For Python/Peewee tests, use the same database variable exposed by starter models.py. If tests refer to models.database, starter models.py must define database = db.",
            "Python tests and starter code must rely on TODO_DATABASE_PATH/current-run SQLite isolation; do not hard-code shared database files or assume a previous test run's schema.",
            "FastAPI tests that depend on startup/lifespan database setup must use with TestClient(app) as client or perform explicit safe db.create_tables setup before requests. Do not create a module-level client = TestClient(app) when the test requires lifespan-created tables.",
            "Python tests must use canonical service names from the starter contract, for example get_todo_by_id and toggle_todo_completion. If a task intentionally requires shorter names such as get_todo or toggle_todo, starter services.py must provide those methods.",
            "If any Python test imports a module or function such as migration.run_migration, that module must be included as an editable starter file with a realistic incomplete implementation.",
            "If Python tests use raw SQL table names with underscores, the corresponding Peewee model must declare the same table_name, for example class AuditLog.Meta.table_name = \"audit_log\".",
            "Starter code must be a task-focused copy or extension of those canonical contracts. Do not output React, Vite, Next.js, ASP.NET, Flask, SQLAlchemy, an in-memory replacement database, or a different Todo base application.",
            "Set the challenge level substantially above a tutorial or basic CRUD exercise. Even easy tasks must require non-trivial reasoning across modules; hard tasks should resemble a compact senior-level take-home exercise.",
            "Reject trivial themes such as a static card, simple list/filter/sort, one-endpoint CRUD, one-query lookup, or an isolated one-line bug.",
            $"Every task must require coordinated changes across at least {minimumStarterFilesPerLanguage} editable starter files for every supported language.",
            "Do not generate progress bars, profile cards, theme toggles, basic forms, simple filters/sorts, static dashboards, counters, or isolated CRUD handlers.",
            "Use flat file names without directories so the browser workspace and sandbox can execute them directly.",
            "Starter files must contain a realistic incomplete codebase with existing contracts, partial implementations, and TODOs. Do not provide the completed solution.",
            "Keep the problem description concise: 80 to 150 words maximum.",
            "State the goal, essential behavior, important edge cases, and acceptance criteria without giving students a copy-ready implementation plan.",
            "Require cross-file behavior, input validation, error handling, state or data-flow consistency, and at least one backward-compatibility or regression constraint.",
            "Name and require at least four advanced engineering concerns appropriate to the task type, such as asynchronous coordination, persistence, state machines, idempotency, authorization, pagination, concurrency, transactions, migrations, rollback, caching, accessibility, auditability, or conflict resolution.",
            "Every task must include at least two public and two hidden test cases. Tests must exercise behavior across the provided files, including edge cases and failure paths.",
            "The platform attaches the administrator-only AI-usage benchmark after generation; do not include student-visible AI grading criteria in the task description.",
            "New admin and student selectable languages are python, javascript, html, and sql. TypeScript is legacy-only and may appear only when Required languages explicitly includes typescript.",
            "Use html for frontend_ui_extension tasks and sql for database_query_schema tasks unless the administrator explicitly asks for another supported language.",
            "For frontend_ui_extension, use index.html, styles.css, and app.js adapted from the canonical browser-safe UI and require accessible interactions, derived state, responsive behavior, and robust empty/error states.",
            "Frontend HTML tests run after the platform loads index.html and app.js into jsdom. Do not create a new JSDOM instance, overwrite global document/window/navigator, or replace document.body in generated tests.",
            "For rest_api_development, use every required language: extend FastAPI/Peewee for Python and Node/Express modules for JavaScript. JavaScript tests must use Jest/Supertest from the platform runtime. Require related routes, validation, consistent errors, and concurrency/idempotency or pagination/filtering concerns.",
            "For database_query_schema, use Python/Peewee/SQLite files from the canonical backend. SQL test helpers are allowed, but the base ORM and database may not be replaced.",
            "For database_query_schema SQL tests, make every raw SQL test_code self-contained: include INSERT/UPDATE/DELETE setup statements before the final SELECT whenever seed.sql alone does not guarantee matching rows. The final SELECT must return at least one row for a correct solution.",
            "For bug_fix, provide at least three interacting Todo application modules in every required language and require diagnosing several related defects while preserving public interfaces and preventing regressions. JavaScript modules and tests must be executable with Node and Jest without npm install.",
            "Every test case must include non-empty test_code for every language in the task's language_constraints.",
            "For database_query_schema tasks, every public and hidden test case must include a non-empty sql test_code entry that verifies the student's solution.sql file.",
            "",
            "JSON shape:",
            "{",
            "  \"tasks\": [",
            "    {",
            "      \"title\": \"string\",",
            "      \"task_type\": \"frontend_ui_extension|rest_api_development|database_query_schema|bug_fix\",",
            "      \"difficulty\": \"easy|medium|hard\",",
            "      \"verification_mode\": \"browser_ui_preview|api_response_check|database_result_check|automated_test|regression_test\",",
            "      \"starter_prototype_reference\": \"string or null\",",
            "      \"problem_description_markdown\": \"string\",",
            "      \"language_constraints\": [\"python\", \"javascript\", \"html\", \"sql\"],",
            "      \"starter_code\": { \"python\": {\"main.py\":\"code\", \"models.py\":\"code\", \"repositories.py\":\"code\", \"services.py\":\"code\", \"controllers.py\":\"code\", \"schemas.py\":\"code\", \"environment.py\":\"code\"}, \"javascript\": {\"server.js\":\"code\", \"models.js\":\"code\", \"repositories.js\":\"code\", \"services.js\":\"code\", \"controllers.js\":\"code\", \"schemas.js\":\"code\", \"environment.js\":\"code\"}, \"html\": {\"index.html\":\"code\", \"styles.css\":\"code\", \"app.js\":\"code\"}, \"sql\": {\"schema.sql\":\"code\", \"seed.sql\":\"code\", \"solution.sql\":\"code\"} },",
            "      \"starter_files_metadata\": { \"language\": {\"file1\":\"editable\", \"file2\":\"editable\", \"file3\":\"editable\"} },",
            "      \"verification_metadata\": {\"primary_view\":\"string\"},",
            "      \"grading_configuration\": {\"runner\":\"automated_tests\", \"requires_student_install\":\"false\"},",
            "      \"traceability_metadata\": {\"requirements\":\"REQ-18f,REQ-18g,REQ-18h,REQ-18i,REQ-18j\"},",
            "      \"max_score\": 25,",
            "      \"test_cases\": [{ \"name\": \"string\", \"visibility\": \"public|hidden\", \"test_code\": {\"python\":\"pytest code\", \"javascript\":\"jest code\"} }]",
            "    }",
            "  ]",
            "}",
            "",
            "Every task must include at least two public test cases and two hidden test cases."
        ]);
    }

    public static string BuildAssessmentTaskPrompt(
        AssessmentRequest request,
        string taskType,
        string difficulty,
        IReadOnlyCollection<string> requiredLanguages,
        int taskNumber,
        int totalTasks)
    {
        return string.Join("\n",
        [
            "Generate exactly one task for a larger assessment draft.",
            $"Task position: {taskNumber} of {totalTasks}",
            $"Required task type: {taskType}",
            $"Required difficulty: {difficulty}",
            $"Required languages: {string.Join(", ", requiredLanguages)}",
            "Return exactly those required languages in language_constraints, starter_code, and every test_code object.",
            "Keep this task inside the default Todo List prototype and its shared data model.",
            "Make it distinct from other likely tasks of the same category by choosing a focused Todo subsystem or engineering concern.",
            "",
            $"Assessment title: {request.Title}",
            $"Assessment description: {request.Description}",
            $"Duration minutes: {request.DurationMinutes}",
            $"Shared prototype reference: {PrototypeDefaults.TodoListReference}",
            $"Shared prototype version: {PrototypeDefaults.TodoListVersion}",
            $"Shared prototype metadata: {JsonDocumentSerializer.Serialize(request.SharedPrototypeMetadata ?? new Dictionary<string, string>())}"
        ]);
    }

    public static string BuildSingleTaskDraftPrompt(
        string taskType,
        string difficulty,
        IReadOnlyCollection<string> requiredLanguages,
        string? administratorGuidance)
    {
        return string.Join("\n",
        [
            "Generate exactly one draft task.",
            $"Task type: {taskType}",
            $"Difficulty: {difficulty}",
            $"Supported languages: {string.Join(", ", requiredLanguages)}",
            $"Shared prototype reference: {PrototypeDefaults.TodoListReference}",
            "The generated task must modify the default Todo List application; unrelated product domains are invalid.",
            $"Administrator guidance: {administratorGuidance ?? "(none supplied)"}"
        ]);
    }
}
