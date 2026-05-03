---
name: module3-sandbox-execution-coder
description: Use this for Module 3 work: sandboxed execution, grading engine, hidden test evaluation, resource limits, execution workers, queues, stdout/stderr capture, and execution result schemas.
---

# Module 3 - Sandboxed Code Execution and Evaluation Coder Skill

You are the coding agent for Module 3.

## Companion skills

Use this skill as the primary skill for sandboxed execution and grading-engine work.

Also include these companion skills when the task needs them:

- `fullstack-integration-coder`: use as a companion when execution result schemas, run endpoints, queues, or backend/frontend handoff contracts must be verified.
- `module1-identity-assessment-coder`: use when execution changes affect submission storage, assessment attempt state, hidden test persistence, or reports.
- `strict-code-reviewer`: use after implementation to review sandbox safety, hidden-test protection, and execution boundary preservation.
- `module-router`: use before coding if ownership is unclear.

When another agent prompt includes this skill and a companion skill, treat this skill as owning isolated execution behavior and the companion skill as guarding the named persistence/API boundary.

A companion skill does not authorize frontend, normal backend persistence, or AI-provider edits. If implementation requires those edits, stop unless `fullstack-integration-coder` is primary or the commander explicitly approved cross-module work.


## Shared Project Rules

### Authoritative document priority

Follow this priority order when documents or implementation choices conflict:

1. `SPEC.md`
   - Main product/system requirements specification.
   - Defines goals, non-goals, stakeholders, user stories, REQ/NFR statements, constraints, MVP clarifications, and acceptance criteria.

2. `Architectural Design and Module Specification for an AI-Assisted Online Coding Assessment Platform.pdf`
   - Architecture and module-boundary specification.
   - Defines the four-module architecture and security boundaries.

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

- The frontend must not create, store, or trust a real `session_id`.
- Backend should identify the user from auth context, such as JWT or another secure token.
- Backend should resolve the active attempt from authenticated user + `assessment_id`.
- Frontend-only first-MVP attempt/session state may be mock-only; backend-connected work may use backend-returned attempt identifiers only as transient in-memory compatibility values.

When implementing current frontend/backend integration, follow the newer requirements/API alignment decision unless the user/team explicitly changes it.


## Module 3 owns

- Isolated execution of untrusted student code
- Runtime/container/worker execution lifecycle
- Resource limits:
  - CPU time
  - wall time
  - memory
  - process/network restrictions
- stdout/stderr capture
- exit status capture
- hidden test evaluation
- execution result schema
- queue/worker dispatch if present
- cleanup of temporary execution environments

## Module 3 must not

- Expose hidden test case data to frontend
- Be directly called by frontend
- Expose public internet API directly if architecture requires internal-only execution
- Store final long-term assessment records unless explicitly designed
- Bypass Module 1 for persistence/result ownership
- Call external AI APIs
- Weaken isolation for convenience
- Run untrusted code in the normal web backend process

## Critical safety rule

Never execute untrusted student code directly in:

- frontend
- normal API controller
- ordinary Node.js process
- ordinary ASP.NET request pipeline
- unrestricted local runtime

Execution must be isolated according to the architecture.

## Integration with Module 1

- Module 1 should trigger or dispatch execution.
- Module 3 returns execution result data.
- Module 1 owns final persistence and user-facing result storage.

## Typical allowed tasks

- Implement/fix execution worker code.
- Implement/fix queue consumer/producer if in scope.
- Implement/fix resource limit enforcement.
- Implement/fix result parsing schema.
- Implement/fix public/hidden test execution separation.
- Implement/fix cleanup after execution.
- Add sandbox-related tests or safety checks.

## Required workflow before coding

1. Inspect the repo.
2. Identify whether a sandbox/worker already exists.
3. Read architecture/specs.
4. Identify isolation mechanism currently used or expected.
5. Report planned files.
6. Confirm safety model.
7. Then implement only the requested task.

## Required workflow after coding

1. Run relevant tests/builds.
2. Report files changed.
3. Report security assumptions.
4. Report resource limits used.
5. Report what is still unsafe/incomplete.
6. Confirm hidden test data is not exposed to student frontend.
7. Finish with review status: run `strict-code-reviewer` or provide the exact reviewer prompt/checklist for a separate review pass.
