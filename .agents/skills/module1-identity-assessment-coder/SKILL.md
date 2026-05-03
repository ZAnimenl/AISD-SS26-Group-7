---
name: module1-identity-assessment-coder
description: Use this for Module 1 work: backend identity, authentication, RBAC, assessment management, questions/test cases persistence, attempts/session lifecycle, workspace persistence, submissions, results, reports, EF Core/PostgreSQL, and related API endpoints.
---

# Module 1 — Identity and Assessment Management Coder Skill

You are the coding agent for Module 1.


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


## Module 1 owns

- Authentication
- Authorization / RBAC
- User accounts and roles
- Assessment CRUD backend
- Question and test case persistence
- Attempt/session lifecycle
- Workspace autosave persistence endpoints
- Submission storage
- Evaluation result storage
- Report aggregation
- Database persistence
- EF Core / PostgreSQL models and migrations when explicitly part of the task
- Backend API contracts for these responsibilities

## Module 1 must protect

- Hidden test cases and expected outputs
- Admin-only notes
- User identity and role permissions
- Scores and report data
- Active attempt lifecycle
- Submission history

## Module 1 must not

- Implement frontend UI
- Directly execute untrusted code inside normal web API routes
- Call external LLM APIs directly unless explicitly part of an approved Module 4 integration
- Return hidden test inputs or hidden expected outputs to student-facing APIs
- Trust frontend-supplied user IDs for authenticated operations
- Trust frontend-managed real `session_id` unless the architecture/team explicitly changes the current decision

## Identity and attempt rules

- Backend should identify the current user from authenticated context, such as JWT or another secure token.
- Backend should resolve active attempt from authenticated user + `assessment_id`.
- Frontend should not be required to create/store/trust a real `session_id`.

## Typical allowed tasks

- Implement/fix auth endpoints.
- Implement/fix student/admin role authorization.
- Implement/fix assessment CRUD backend.
- Implement/fix question/test-case persistence.
- Implement/fix attempt start/resume/expiry behavior.
- Implement/fix workspace persistence endpoints.
- Implement/fix submission storage and result storage.
- Implement/fix report aggregation.
- Add/update EF Core migrations when explicitly required.
- Add/update backend tests.

## Required workflow before coding

1. Inspect the repo.
2. Identify backend framework and solution structure.
3. Read relevant specs.
4. Identify exact API contract involved.
5. Report planned files.
6. Confirm whether DB/migration changes are necessary.
7. Then implement only the requested task.

## Required workflow after coding

1. Run backend build/tests if available.
2. Run frontend build/tests if frontend contract was touched.
3. Report files changed.
4. Report API contract changes.
5. Report migrations, if any.
6. Confirm hidden-test protection.
7. Confirm auth/RBAC behavior.
