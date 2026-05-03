# AISD-SS26 Group 7 🚀

AI-assisted online coding assessment platform for browser-based coding assessments, role-based administration, backend-backed workspace persistence, submission evaluation, AI assistance, and reporting.

## Project Shape 🧭

This repository contains a Next.js frontend and an ASP.NET backend:

- `src/` - Next.js App Router frontend, student/admin pages, browser IDE workspace, frontend API client, shared frontend types.
- `Backend/Backend/` - ASP.NET backend API, auth, assessments, sessions/attempts, workspace persistence, submissions, reports, AI endpoint stubs, and execution endpoints.
- `Backend/OjSharp.Tests/` - backend contract and service tests.
- `.agents/skills/` - local agent skills for planning, implementation, integration, review, and handoff.

Authoritative project documents:

- `requirements.md`
- `Architectural Design and Module Specification for an AI-Assisted Online Coding Assessment Platform.pdf`
- `complete_frontend_api_list_and_backend_alignment.md`
- `module2_frontend_ui_task.md`
- `ui-style-reference.md`

Do not edit those specification files unless the task explicitly asks for documentation/spec changes.

## Architecture Boundaries 🏗️

The project follows four module boundaries:

1. Module 1 - Identity and Assessment Management
   Auth, RBAC, users, assessments, questions/test cases, attempts, workspace persistence, submissions, reports, and database-backed state.

2. Module 2 - Interactive Browser-Based Workspace / Frontend IDE
   Browser UI, Monaco/editor integration, student/admin pages, autosave UI, run/submit UI, output console, AI assistant UI, and frontend API clients.

3. Module 3 - Sandboxed Code Execution and Evaluation
   Isolated execution, resource limits, hidden test evaluation, stdout/stderr capture, workers/queues, and cleanup.

4. Module 4 - AI Telemetry and Assistance
   Secure AI proxy/service, provider calls, AI logging, telemetry, semantic tags, structured AI responses, and rate/error handling.

Security rules 🔒:

- Student frontend must never receive hidden test inputs, hidden expected outputs, or grading implementation.
- Frontend must not access the database, sandbox, or external AI providers directly.
- Do not execute student submissions locally with `eval`, `child_process`, Docker, or unrestricted runtimes.
- Frontend must not create, store, or trust a real `session_id`; current backend session-shaped values are treated as transient backend attempt IDs only.

## Prerequisites 🧰

- Node.js 20+
- npm
- .NET SDK compatible with `Backend/Backend.sln`
- PostgreSQL for backend local development

Local backend development expects PostgreSQL at:

```text
Host=localhost:5433;Database=ai_coding;Username=ai_coding;password=password
```

The backend default local URL is:

```text
http://localhost:5040
```

The frontend API client defaults to:

```text
http://localhost:5040/api/v1
```

and falls back to:

```text
http://localhost:5041/api/v1
```

Override with `NEXT_PUBLIC_API_BASE_URL` if needed.

## Environment Files 🌱

An example environment file is provided for handoff clarity:

- `.env.example` - frontend and backend configuration examples.

For local development, most backend values already come from `Backend/Backend/appsettings.Development.json`. ASP.NET Core also reads environment variables automatically, but it does not load `.env` files by itself unless your shell, container, or tooling loads them.

Things users may configure:

- `NEXT_PUBLIC_API_BASE_URL` - frontend API base URL. Optional for normal local development.
- `ASPNETCORE_ENVIRONMENT` - backend environment, usually `Development` locally.
- `BackendUrls` - backend listen URL, defaulting to `http://localhost:5040`.
- `ConnectionStrings__DefaultConnection` - PostgreSQL connection string.
- `SeedAdmin__Email` and `SeedAdmin__Password` - required for published/production environments because production `appsettings.json` intentionally omits seed admin credentials.

AI provider keys:

- No AI provider API key is required today.
- The current backend AI chat endpoint is an MVP stub that logs interactions and returns canned guidance.
- Future Module 4 provider work may introduce keys such as `OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, or `GEMINI_API_KEY`; these are intentionally commented as placeholders in `.env.example`.

## Frontend 💻

Install dependencies:

```powershell
npm install
```

Run the Next.js dev server:

```powershell
npm run dev
```

Verify the frontend:

```powershell
npm run typecheck
npm run build
```

Useful routes:

- `/login`
- `/student/dashboard`
- `/student/assessments`
- `/student/assessments/[assessmentId]/start`
- `/student/assessments/[assessmentId]/workspace`
- `/student/results`
- `/admin/dashboard`
- `/admin/assessments`
- `/admin/reports`
- `/admin/users`

The student workspace uses Monaco through `@monaco-editor/react`.

## Backend ⚙️

Run backend build:

```powershell
dotnet build Backend\Backend.sln -v:minimal
```

Run backend tests:

```powershell
dotnet test Backend\Backend.sln -v:minimal
```

Run the backend API:

```powershell
dotnet run --project Backend\Backend\Backend.csproj
```

Local demo users are seeded from development configuration:

- Admin: `admin@example.com`
- Student: `student@example.com`
- Password: `password`

## API Notes 🔌

Frontend API calls should go through:

```text
src/lib/api/index.ts
```

Important connected flows include:

- auth login/logout/me/register
- student dashboard, assessments, results
- admin dashboard, assessments, users, reports
- workspace context/load/autosave
- run, submit, AI chat
- question and test case management

Known contract bridge:

- The alignment docs prefer auth-context + `assessment_id` attempt resolution.
- The current backend still exposes some session-shaped endpoints.
- The frontend must keep backend attempt IDs transient in memory and must not persist them as authoritative frontend session state.

## Agent Skills 🧠

Local agent skills live under:

```text
.agents/skills/
```

Use `AGENTS.md` as the orchestration guide.

Common skill chains:

- Planning a coding task: `prompt-commander` -> module coder -> `strict-code-reviewer`
- Frontend IDE/UI work: `module2-frontend-ide-coder`
- Frontend work with backend API contract risk: `module2-frontend-ide-coder` + companion `fullstack-integration-coder`
- Cross-module implementation: primary `fullstack-integration-coder`
- Review: `strict-code-reviewer`
- Context handoff: `handoff-summary`

Companion skills provide boundary checks and contract awareness only. They do not expand implementation scope unless the commander explicitly marks them as primary or approves cross-module work.

## Recommended Checks Before Handoff ✅

Frontend:

```powershell
npm run typecheck
npm run build
```

Backend:

```powershell
dotnet build Backend\Backend.sln -v:minimal
dotnet test Backend\Backend.sln -v:minimal
```

Safety scans:

```powershell
rg "mock-api|@/mocks|ojsharp.assessment.session" src
rg "eval\(|child_process|docker|openai|anthropic|gemini" src Backend
```

Expected safety posture:

- No runtime mock API imports.
- No frontend-managed stored assessment session ID.
- No local execution of student submissions.
- No direct external AI provider calls from frontend.
- No hidden test input/output in student UI.
