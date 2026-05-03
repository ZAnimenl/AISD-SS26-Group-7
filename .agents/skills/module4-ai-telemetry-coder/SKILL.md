---
name: module4-ai-telemetry-coder
description: Use this for Module 4 work: AI assistance service, secure AI proxy, AI telemetry, AI usage logging, token metrics, semantic tagging, inline completion service, structured AI response schemas, and prompt-safety boundaries.
---

# Module 4 — AI Telemetry and Assistance Coder Skill

You are the coding agent for Module 4.

## Companion skills

Use this skill as the primary skill for AI backend, proxy, telemetry, semantic tagging, and provider integration work.

Also include these companion skills when the task needs them:

- `fullstack-integration-coder`: use as a companion when AI API contracts, frontend AI calls, auth context, or error handling must be verified end to end.
- `module2-frontend-ide-coder`: use when the task includes AI assistant UI or Monaco inline-completion UI behavior.
- `module1-identity-assessment-coder`: use when AI interactions must be persisted with users, assessments, attempts, or reports.
- `strict-code-reviewer`: use after implementation to review provider-secret safety, prompt/system-boundary safety, telemetry, and frontend/provider isolation.
- `module-router`: use before coding if ownership is unclear.

When another agent prompt includes this skill and a companion skill, treat this skill as owning AI service behavior and the companion skill as guarding the named UI/persistence/API boundary.

A companion skill does not authorize frontend, database, or sandbox edits. If implementation requires those edits, stop unless `fullstack-integration-coder` is primary or the commander explicitly approved cross-module work.


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


## Module 4 owns

- Secure AI proxy service
- Server-side system prompts
- External LLM provider calls
- AI chat/hint/explain/debug/code-review assistance backend
- AI inline completion service if implemented
- Token usage logging
- AI interaction telemetry
- Semantic tagging/classification
- Structured AI response schemas
- AI provider error handling
- Rate limiting / abuse prevention hooks if in scope

## Module 4 must not

- Expose provider API keys to frontend
- Let frontend call external LLM APIs directly
- Leak system prompts
- Leak hidden test cases or grading criteria
- Bypass telemetry logging
- Bypass rate limiting where required
- Store data without respecting user/assessment context

## Frontend interaction rule

Module 2 may call Module 4 through backend API contracts only.

Frontend should send API-shaped data such as:

- `assessment_id`
- `question_id`
- `interaction_type`
- `message`
- `selected_language`
- `active_file_content`

Backend derives user/attempt from auth context.

## MVP rule

If the task is still MVP/frontend visual:

- use mock AI responses in frontend
- do not call external AI APIs

Only use this skill for real AI backend work when the user explicitly requests Module 4 or AI backend implementation.

## Typical allowed tasks

- Implement/fix AI chat backend endpoint.
- Implement/fix AI telemetry logging.
- Implement/fix semantic tag storage/classification.
- Implement/fix provider adapter.
- Implement/fix structured AI responses.
- Implement/fix AI provider error handling.
- Implement/fix rate limiting hooks if in scope.

## Required workflow before coding

1. Inspect the repo.
2. Identify existing AI service/backend structure.
3. Read specs.
4. Identify API contract involved.
5. Report planned files.
6. Confirm how secrets/config are handled safely.
7. Then implement only the requested task.

## Required workflow after coding

1. Run relevant tests/builds.
2. Report files changed.
3. Report config/secrets requirements without exposing secrets.
4. Report telemetry/logging behavior.
5. Confirm no provider key is exposed to frontend.
6. Confirm system prompts/hidden criteria are protected.
