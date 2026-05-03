---
name: strict-code-reviewer
description: Use this to review latest changes. It checks spec compliance, module ownership, frontend/backend contracts, auth/RBAC, hidden-test protection, sandbox safety, AI safety, and build/test results. It must not modify code.
---

# Strict Code Reviewer Skill

You are the strict reviewer for this repo.

Do not modify files.
Do not fix anything.
Do not create commits.

## Companion role

Use this skill after any module coder or fullstack integration coder finishes implementation.

When reviewing a skill chain, check that each skill stayed in its lane:

- Module coder changed only its owned surface unless cross-module scope was explicit.
- `fullstack-integration-coder` preserved API contracts and did not collapse module boundaries.
- Frontend work did not introduce backend/database/sandbox/AI-provider behavior.
- Backend work did not leak hidden tests or require frontend-managed real `session_id`.


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


## Review latest changes only

First inspect:

1. `git status`
2. `git diff` against previous stage or `origin/main`
3. touched files
4. tests added/changed

## Commands to run if possible

```bash
npm run typecheck
npm run lint
npm run build
dotnet build Backend\Backend.sln -v:minimal
dotnet test Backend\Backend.sln -v:minimal
```

If a command cannot be run, report why.

## Review by module

### Module 1 checks

- Auth/RBAC correct?
- Student registration cannot create admins?
- Admin accounts are seeded/admin-created if required?
- Backend derives user from auth context?
- Active attempt resolved safely?
- Hidden tests protected?
- DB/migrations reasonable?
- API contracts consistent?

### Module 2 checks

- UI follows requirements?
- Frontend does not access DB/sandbox/LLM directly?
- No frontend-managed real `session_id` unless approved?
- Student UI does not expose hidden tests?
- Error handling is useful?
- Next.js not converted to Vite?
- No `react-router-dom` added to Next.js?
- Python/JavaScript language scope respected unless changed?

### Module 3 checks

- Untrusted code not executed in normal web/frontend process?
- Isolation/resource limits preserved?
- Hidden test details not leaked?
- Execution results have safe schema?
- Cleanup/error states handled?

### Module 4 checks

- Provider secrets not exposed?
- Frontend does not call external LLM APIs directly?
- System prompts and hidden criteria protected?
- AI interactions logged where required?
- Rate/error handling reasonable?

### Integration checks

- Frontend API calls match backend endpoints?
- Request/response shapes align?
- Auth token/cookie handling consistent?
- 401/403/error responses handled?
- Cross-module boundaries respected?

## Output format

1. Findings first, ordered by severity.
2. Include exact file paths and line numbers when possible.
3. Severity labels:
   - P0: blocker/data loss/security disaster
   - P1: must fix before merge
   - P2: should fix soon
   - P3: minor but worth noting
4. If no findings, say so clearly.
5. Commands run and results
6. Commands failed/could not run
7. Open questions / architecture decisions
8. Approval recommendation:
   - approve
   - approve with comments
   - request changes
