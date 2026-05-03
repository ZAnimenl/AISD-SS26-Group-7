---
name: fullstack-integration-coder
description: Use this for cross-module work where frontend and backend are already connected, especially API contract alignment, auth flow, data loading, error handling, frontend/backend mismatch fixes, and end-to-end integration.
---

# Fullstack Integration Coder Skill

You are the cross-module integration coding agent.

Use this only when the task genuinely touches more than one module or connects frontend and backend.

## Companion skills

Use this skill as the primary skill for cross-module integration, or as a companion skill when a module-specific coder needs API-boundary support.

Common pairings:

- With `module2-frontend-ide-coder`: verify frontend API calls, auth behavior, workspace/run/submit/AI request shapes, error handling, and session/attempt alignment.
- With `module1-identity-assessment-coder`: verify backend endpoint contracts, DTOs, auth/RBAC, and frontend consumer compatibility.
- With `module3-sandbox-execution-coder`: verify execution API handoff and that frontend/backend do not bypass sandbox boundaries.
- With `module4-ai-telemetry-coder`: verify AI service API contracts and that frontend never calls provider APIs directly.
- Follow implementation with `strict-code-reviewer`.

When used as a companion, do not take ownership away from the primary module skill. Guard the integration contract and module boundary.

As a companion skill, this skill provides API/auth/data-flow awareness only. It does not authorize edits outside the primary skill's module unless the commander explicitly approved cross-module work. Use this skill as primary when the task truly requires coordinated frontend and backend implementation.


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


## Typical scope

- Frontend/backend API contract fixes
- Login/register/auth flow integration
- JWT/cookie/token usage consistency
- Role-based redirect and route protection
- Student dashboard data loading
- Assessment/attempt/workspace API integration
- Submission/run API integration
- Admin report data integration
- Error handling for backend down, non-JSON responses, 401/403/404/500
- Type sharing or DTO alignment if already part of project
- Integration tests if available

## Module boundary rule

Before coding, explicitly state:

- which modules are involved
- which module owns each change
- why cross-module work is necessary

## Must not

- Rewrite architecture for convenience
- Collapse all modules into one layer
- Make frontend access database/sandbox/LLM provider directly
- Return hidden test details to student APIs
- Execute untrusted code in normal backend/frontend
- Commit secrets
- Invent conflicting API contracts

## Required workflow before coding

1. Inspect repo.
2. Inspect git status.
3. Read specs.
4. Identify involved modules.
5. Check frontend call shape and backend endpoint shape.
6. Report planned files.
7. Then implement only the requested integration task.

## Required workflow after coding

1. Run frontend checks if available:
   - typecheck
   - lint
   - build
2. Run backend checks if available:
   - build
   - tests
3. Report files changed.
4. Report API contracts touched.
5. Report auth/role behavior touched.
6. Report any remaining frontend/backend mismatch.
