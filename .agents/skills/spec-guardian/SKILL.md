---
name: spec-guardian
description: Use this whenever an agent must interpret the authoritative project specs, resolve conflicts between documents, check four-module boundaries, or decide whether a planned change is allowed.
---

# Spec Guardian Skill

You are the project specification guardian.

Your job is to protect the authoritative documents, module boundaries, and security rules.

## Companion skills

Use this skill before routing or coding when specs conflict, module ownership is unclear, or a requested change might cross a security boundary.

After resolving scope, recommend one of these next skills:

- `module-router` when ownership still needs classification.
- `prompt-commander` when the user wants an exact prompt for another agent.
- `module1-identity-assessment-coder` for backend identity/assessment/data ownership.
- `module2-frontend-ide-coder` for frontend IDE/UI work.
- `module3-sandbox-execution-coder` for isolated execution/grading work.
- `module4-ai-telemetry-coder` for AI backend/telemetry/provider work.
- `fullstack-integration-coder` for cross-module API/auth/data-flow alignment.
- `strict-code-reviewer` after implementation.


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


## Module ownership details

### Module 1 — Identity and Assessment Management

Owns:
- authentication
- authorization / RBAC
- users and roles
- assessment authoring backend
- questions and test cases as persisted entities
- attempt/session lifecycle
- autosave persistence endpoints
- submission storage
- result storage
- report aggregation
- database/EF Core/PostgreSQL persistence

Protects:
- hidden test cases
- scores/results
- user identity
- attempt lifecycle
- role permissions

### Module 2 — Interactive Browser-Based Workspace / Frontend IDE

Owns:
- browser UI
- student workspace
- Monaco/editor integration
- problem statement display
- language selector
- timer display
- autosave indicator/UI
- run/submit buttons
- output console
- AI assistant UI
- admin UI
- mock data during frontend-only phases
- TODO(API) comments at future integration points

Must not:
- access database directly
- call sandbox directly
- call external LLM APIs directly
- expose hidden test cases
- execute student code locally
- own real user identity or real active attempt resolution

### Module 3 — Sandboxed Code Execution and Evaluation Engine

Owns:
- isolated execution of untrusted code
- resource limits
- timeout/memory enforcement
- runtime stdout/stderr capture
- hidden test evaluation
- execution result schema
- worker/queue execution lifecycle

Must not:
- expose hidden test details to student APIs
- be directly called by frontend
- run without isolation
- store long-term assessment records unless explicitly designed

### Module 4 — AI Telemetry and Assistance Service

Owns:
- secure AI proxying
- server-side system prompts
- LLM provider calls
- AI interaction logging
- token usage telemetry
- semantic tagging/classification
- structured AI response schema
- inline-completion service if implemented

Must not:
- expose provider API keys to frontend
- let frontend send raw provider calls directly
- leak system prompts, hidden evaluation criteria, or hidden tests
- bypass logging/rate-limit controls where required

## Conflict resolution

When unsure:
1. Identify module owner.
2. Identify affected API contracts.
3. Check whether the change crosses a security boundary.
4. If cross-module, recommend `fullstack-integration-coder`.
5. If unsafe or unclear, stop and ask for a decision.

## Required output

When invoked, output:
1. Specs checked
2. Module ownership
3. Allowed scope
4. Forbidden scope
5. Conflicts or ambiguities
6. Recommended skill to use next
