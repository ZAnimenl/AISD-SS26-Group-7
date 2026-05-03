---
name: module-router
description: Use this before coding when it is unclear which of the four architecture modules owns a task. It routes work to Module 1, Module 2, Module 3, Module 4, or cross-module integration.
---

# Module Router Skill

You decide which architecture module should own a requested task.

Do not modify files.


## Shared Project Rules

### Authoritative document priority

Follow this priority order when documents or implementation choices conflict:

1. `requirements.md`
   - Main product/system requirements specification.
   - Defines goals, non-goals, stakeholders, user stories, REQ/NFR statements, constraints, MVP clarifications, and acceptance criteria.

2. `Architectural Design and Module Specification for an AI-Assisted Online Coding Assessment Platform.pdf`
   - Architecture and module-boundary specification.
   - Defines the four-module architecture and security boundaries.

3. `complete_frontend_api_list_and_backend_alignment.md`
   - Frontend/backend API contract and integration alignment document.
   - Defines endpoint names, request/response shapes, status values, error format, MVP API decisions, mock/TODO(API) rules, and frontend-backend boundaries.

4. `module2_frontend_ui_task.md`, if present
   - Module 2 task breakdown.

5. `ui-style-reference.md`
   - Visual style reference only.
   - It must not override requirements, architecture, authentication behavior, database schema, existing routes, assessment/submission/reporting behavior, or API contracts.

### Do not modify specification documents unless explicitly asked

Do not modify these files unless the user explicitly asks for documentation/spec changes:

- `requirements.md`
- `Architectural Design and Module Specification for an AI-Assisted Online Coding Assessment Platform.pdf`
- `complete_frontend_api_list_and_backend_alignment.md`
- `module2_frontend_ui_task.md`
- `ui-style-reference.md`

If a coding task conflicts with these documents, stop and report the conflict before editing implementation files.

### Current project architecture assumption

The project is an AI-assisted online coding assessment/interview platform.

Known current implementation context may include:

- Frontend: Next.js App Router in `src/`
- Backend: ASP.NET / .NET backend in `Backend/`
- Database: PostgreSQL via EF Core
- Auth: JWT/token-based login/register

Do not assume these blindly. Inspect the repository first and report what you find.

### Four-module architecture

The architecture has four non-overlapping modules:

1. **Module 1 — Identity and Assessment Management**
   - Authentication, RBAC, users, assessments, questions/test cases, attempt/session lifecycle, workspace persistence, submissions, results, reports, and database-backed authoritative state.

2. **Module 2 — Interactive Browser-Based Workspace / Frontend IDE**
   - Browser UI, student/admin pages, Monaco/editor, workspace state UI, autosave UI, run/submit UI, AI assistant UI, frontend API clients, and visual interaction layer.

3. **Module 3 — Sandboxed Code Execution and Evaluation Engine**
   - Isolated execution of untrusted code, resource limits, hidden test evaluation, stdout/stderr capture, execution result schema, workers/queues, cleanup.

4. **Module 4 — AI Telemetry and Assistance Service**
   - Secure AI backend/proxy, LLM provider calls, server-side prompts, AI interaction logging, telemetry, semantic tagging, structured AI responses, rate/error handling.

### Global security boundaries

- Student frontend must never receive hidden test case input/output or grading implementation.
- Frontend must not directly access the database.
- Frontend must not directly call the sandbox/execution engine.
- Frontend must not directly call external LLM/AI provider APIs.
- Do not expose provider API keys, database credentials, JWT secrets, or other secrets.
- Do not execute untrusted student code outside the intended isolated execution architecture.
- Do not use `eval`, `child_process`, or unrestricted local runtimes for student submissions.
- Do not weaken authentication or role-based access control.
- Do not invent API contracts that conflict with the API alignment document.
- Do not collapse module boundaries for convenience.

### Important session/attempt clarification

The architecture PDF contains older schema examples using `session_id`.
However, the current requirements/API alignment decision says:

- The frontend must not create, store, or trust a real `session_id`.
- Backend should identify the user from auth context, such as JWT or another secure token.
- Backend should resolve the active attempt from authenticated user + `assessment_id`.
- Any frontend first-MVP attempt/session state must be mock-only.

When implementing current frontend/backend integration, follow the newer requirements/API alignment decision unless the user/team explicitly changes it.


## Routing rules

### Route to Module 1 if the task mentions:
- auth
- login/register backend
- JWT
- users
- roles
- RBAC
- assessment CRUD backend
- question/test-case persistence
- attempts
- session lifecycle
- autosave persistence
- submission storage
- result storage
- report aggregation
- EF Core
- PostgreSQL
- migrations
- database models
- backend API ownership

Use: `module1-identity-assessment-coder`

### Route to Module 2 if the task mentions:
- frontend
- UI
- browser IDE
- Monaco
- editor
- dashboard
- Next.js routes/pages
- mock data
- student workspace
- admin UI
- visual polish
- autosave indicator
- run/submit button UI
- output console
- AI assistant panel UI
- frontend API client behavior

Use: `module2-frontend-ide-coder`

### Route to Module 3 if the task mentions:
- sandbox
- code execution
- grading engine
- hidden test evaluation
- timeout
- memory limit
- stdout/stderr capture
- execution worker
- queue
- container isolation
- nsjail
- Judge0-like execution
- untrusted code runtime

Use: `module3-sandbox-execution-coder`

### Route to Module 4 if the task mentions:
- AI service backend
- LLM provider
- OpenAI/Anthropic API
- AI telemetry
- token usage
- semantic tags
- AI interaction logging
- inline completion service
- system prompts
- structured AI responses
- prompt safety
- AI proxy

Use: `module4-ai-telemetry-coder`

### Route to fullstack integration if the task mentions:
- frontend/backend mismatch
- API contract drift
- connecting frontend to backend
- end-to-end flow
- login flow between frontend and backend
- data loading from backend
- integration tests
- request/response shape mismatch
- CORS/error handling across frontend/backend

Use: `fullstack-integration-coder`

## Required output

1. Task summary
2. Primary module
3. Secondary affected modules, if any
4. Recommended coding skill
5. Forbidden changes
6. First safe prompt to use
