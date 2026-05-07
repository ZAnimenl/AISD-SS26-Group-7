---
name: module2-frontend-ide-coder
description: Use this for Module 2 work: frontend UI, browser-based IDE, Monaco/editor, dashboards, admin/student pages, frontend API integration, mock data, visual polish, and frontend-only workspace behavior.
---

# Module 2 - Frontend IDE Coder Skill

You are the coding agent for Module 2.

## Companion skills

Use this skill as the primary skill for frontend IDE/UI work.

Also include these companion skills when the task needs them:

- `fullstack-integration-coder`: use as a companion when frontend changes touch live backend API contracts, auth flow, workspace/run/submit/AI request shapes, or frontend/backend mismatch fixes.
- `strict-code-reviewer`: use after implementation to review spec compliance, hidden-test protection, frontend/backend contracts, and build results.
- `module-router`: use before coding if ownership is unclear.

When another agent prompt includes this skill and a companion skill, treat this skill as owning frontend files and the companion skill as guarding the boundary it names.

A companion skill does not authorize backend, database, sandbox, or AI-provider edits. If implementation requires those edits, stop unless `fullstack-integration-coder` is primary or the commander explicitly approved cross-module work.


## Shared Project Rules

### Authoritative document priority

Follow this priority order when documents or implementation choices conflict:

1. `SPEC.md`
   - Main product/system requirements specification.
   - Defines goals, non-goals, stakeholders, user stories, REQ/NFR statements, constraints, MVP clarifications, and acceptance criteria.

2. `Architectural Design and Module Specification for an AI-Assisted Online Coding Assessment Platform.pdf`
   - Architecture and module-boundary specification.
   - Defines the four-module architecture and security boundaries.
   - Some endpoint/schema examples are older. For current assessment attempt, workspace, run, submit, and AI API routes, follow `SPEC.md` and `complete_frontend_api_list_and_backend_alignment.md`.

3. `complete_frontend_api_list_and_backend_alignment.md`
   - Frontend/backend API contract and integration alignment document.
   - Defines endpoint names, request/response shapes, status values, error format, MVP API decisions, mock/TODO(API) rules, and frontend-backend boundaries.

4. `ui-style-reference.md`
   - Visual style reference only.
   - It must not override requirements, architecture, authentication behavior, database schema, existing routes, assessment/submission/reporting behavior, or API contracts.


### Do not modify specification documents unless explicitly asked

Do not modify these files unless the user explicitly asks for documentation/spec changes:

- `SPEC.md`
- `Architectural Design and Module Specification for an AI-Assisted Online Coding Assessment Platform.pdf`
- `complete_frontend_api_list_and_backend_alignment.md`
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

1. **Module 1 - Identity and Assessment Management**
   - Authentication, RBAC, users, assessments, questions/test cases, attempt/session lifecycle, workspace persistence, submissions, results, reports, and database-backed authoritative state.

2. **Module 2 - Interactive Browser-Based Workspace / Frontend IDE**
   - Browser UI, student/admin pages, Monaco/editor, workspace state UI, autosave UI, run/submit UI, AI assistant UI, frontend API clients, and visual interaction layer.

3. **Module 3 - Sandboxed Code Execution and Evaluation Engine**
   - Isolated execution of untrusted code, resource limits, hidden test evaluation, stdout/stderr capture, execution result schema, workers/queues, cleanup.

4. **Module 4 - AI Telemetry and Assistance Service**
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

- The frontend must not create, store, trust, or send a real `session_id`.
- Backend should identify the user from auth context, such as JWT or another secure token.
- Backend should resolve the active attempt from authenticated user + `assessment_id`.
- Frontend-only first-MVP attempt/session state may be mock-only. Backend-connected workspace, run, submit, and AI flows are assessment-scoped and must not send `session_id` or `attempt_id`.

When implementing current frontend/backend integration, follow the newer requirements/API alignment decision unless the user/team explicitly changes it.


## Module 2 owns

- Browser UI
- Student dashboard
- Student assessment list
- Student result page
- Student assessment start page
- Embedded browser IDE workspace
- Monaco/editor integration
- Problem statement panel
- Question list
- Language selector
- Timer display
- Autosave indicator
- Output console
- Run and Submit UI
- AI assistant UI
- Admin dashboard UI
- Assessment/question/test-case/report UI
- Frontend API clients
- Mock data during MVP/frontend-only phases
- TODO(API) comments

## Module 2 must not

- Access database directly
- Implement backend persistence
- Implement real JWT verification
- Execute student code locally
- Use `eval`
- Use `child_process`
- Use Docker/runtime execution for submissions
- Directly call sandbox
- Directly call external LLM APIs
- Expose hidden test cases in student UI
- Trust, create, or send a real frontend-managed `session_id`

## Frontend/backend integration rules

If frontend is connected to backend:

- Use existing backend API contracts.
- If request/response shapes or endpoint names are uncertain, consult `fullstack-integration-coder` guidance before editing.
- Do not invent endpoints if backend already has routes.
- Handle backend-down, non-JSON, 401, 403, 404, and 500 errors gracefully.
- Preserve auth token/cookie behavior already used by the project.
- Do not change backend code unless the user explicitly chooses cross-module/fullstack work.

## Embedded browser IDE architecture

The IDE should have clear frontend boundaries:

- route/page layer
- workspace shell
- editor adapter/component
- problem panel
- question navigation
- language selector
- autosave state
- output console
- run/submit controls
- AI assistant panel
- frontend API/mock API layer
- shared types

If Monaco is used:

- isolate Monaco-specific code in dedicated components/adapters
- prepare for model/URI based virtual files when practical
- avoid destructive updates that break undo/redo when possible

If Monaco is not feasible:

- build polished fallback editor
- add `TODO(Monaco)`

## Student language rule

First MVP student submission languages:

- Python
- JavaScript

Do not show TypeScript as a student submission language unless the team explicitly expands scope.

## Hidden test rule

Student UI may show:

- public/sample test results
- hidden test summary counts after submit, if allowed

Student UI must never show:

- hidden test inputs
- hidden expected outputs
- grading implementation

## Typical allowed tasks

- Create/improve frontend pages.
- Create/improve browser IDE UI.
- Add Monaco integration or editor fallback.
- Add frontend API client integration.
- Improve frontend error/loading/empty states.
- Add role-aware frontend navigation.
- Add UI polish based on `ui-style-reference.md`.
- Add mock data where appropriate.

## Required workflow before coding

1. Inspect the repo.
2. Identify frontend framework.
3. Read specs.
4. Identify whether task is frontend-only or integration.
5. Report planned files.
6. Then implement only the requested task.

## Required workflow after coding

1. Run typecheck/lint/build if available.
2. Report files changed.
3. Report pages/components changed.
4. Report API integration or TODO(API) points.
5. Confirm no forbidden backend/database/sandbox/real AI changes.
6. Finish with review status: run `strict-code-reviewer` or provide the exact reviewer prompt/checklist for a separate review pass.
