# Final Implementation Goals

## Document Status

This document records the final target state for the updated AISD-SS26-Group-7 platform implementation. It is a planning and handoff artifact for future coding work.

It does not replace the authoritative specification set. If this document conflicts with `SPEC.md`, the architecture document, `complete_frontend_api_list_and_backend_alignment.md`, or `AGENTS.md`, follow those authoritative documents in their configured priority order.

This document intentionally describes final product goals only. It does not treat implementation phases as product goals.

## Primary Product Goal

Build a web-based AI-assisted coding assessment platform where administrators create and review practical development assessments, students complete those assessments inside a browser-based IDE, code is executed and graded safely in a sandbox, AI assistance is embedded directly into the workspace, and administrators can evaluate both coding results and AI token-efficiency behavior.

The completed implementation must satisfy the updated `SPEC.md` across assessment authoring, shared-prototype task content, student workspace behavior, task-specific verification, sandboxed run/submit flows, administrator reporting, embedded AI assistance, AI telemetry, and token analytics.

## Core Product Direction

The platform must evaluate realistic development work, not LeetCode-style algorithm tasks.

Assessments should be based on practical tasks derived from a shared runnable prototype. Students work with platform-managed starter files and task metadata inside the assessment website. They must not be expected to clone a project, install dependencies, start local services, or configure a local database.

The first shared-prototype assessment uses the Todo prototype as source material only. The full external Todo project must not be copied directly into the app as a student-run repository. It should be converted into platform-native assessment content: starter files, task metadata, supported languages, verification modes, public tests, hidden tests, and preview or output harnesses.

The existing Product Dashboard demo may be replaced during the shared-prototype implementation, but it must be replaced deliberately and not deleted casually without preserving the intended workspace and assessment behavior.

## Assessment Authoring Goals

Administrators must be able to create, edit, publish, close, archive, or delete assessments with titles, descriptions, durations, statuses, AI-agent enablement, task ordering, and task metadata.

Manual administrator-created assessments must remain flexible. An administrator can choose the number, order, and type of tasks in an assessment.

AI-generated full assessment drafts must default to exactly four tasks, one from each supported primary task category:

| Task type ID | Final task category |
| --- | --- |
| `frontend_ui_extension` | Frontend UI extension |
| `rest_api_development` | REST API development |
| `database_query_schema` | Database query or schema work |
| `bug_fix` | Bug fixing in existing code |

The following legacy aliases must continue to resolve safely:

| Legacy alias | Canonical task type ID |
| --- | --- |
| `web_application` | `frontend_ui_extension` |
| `api_development` | `rest_api_development` |
| `database_task` | `database_query_schema` |

LLM-generated assessment tasks and test cases are drafts only. They must remain administrator-reviewable and editable before publication. The system should preserve provenance where practical: manually authored, LLM-generated, or administrator-edited after LLM generation.

## Shared Prototype Content Goals

The platform must represent shared-prototype assessment content as platform-managed data, not as a student-local project setup.

Each task should contain or reference:

- title and focused task description
- canonical task type ID
- supported language metadata
- difficulty metadata
- starter prototype reference
- editable starter files
- verification mode
- grading configuration
- public tests for run feedback
- hidden tests for final submission
- administrator-only notes where needed
- traceability metadata where practical

The Todo shared-prototype assessment must provide four focused tasks:

- one frontend UI extension task
- one REST API development task
- one database query or schema task
- one bug-fixing task

Each task must be small enough to complete in the browser workspace with platform-managed execution and embedded AI assistance.

## Student Workspace Goals

Students must be able to open an assessment, view all available tasks, switch between tasks, read task descriptions, inspect the shared prototype file tree, edit multiple files, see the task type and verification mode, run public checks, and submit final solutions.

The workspace must support at least Python and JavaScript as student submission languages. TypeScript may appear as starter or prototype material only when the implementation supports the corresponding task flow without expanding student submission language requirements beyond the specification.

The frontend must not create, store, trust, or send a real `session_id` or `attempt_id`. Backend-connected assessment flows must be assessment-scoped, and the backend must resolve the active attempt from the authenticated user and assessment ID.

Student code must be preserved during normal page interactions and scoped to the authenticated user, assessment, and task.

## Preview and Verification Goals

Each task must expose a verification experience appropriate to its category:

| Task category | Final verification goal |
| --- | --- |
| Frontend UI extension | Direct browser UI preview of the relevant prototype surface |
| REST API development | Endpoint response output, API checks, or automated API test output |
| Database query/schema | Query result tables, schema validation, or database-oriented test output |
| Bug fixing | Regression output appropriate to the defect, such as UI, API, database, unit-test, or regression-test results |

Only frontend UI extension tasks require a direct browser preview. Other task categories may use task-specific output or test results as the primary verification view.

Run feedback should use public/sample checks. Submit feedback should use hidden tests without exposing hidden inputs, hidden expected outputs, grading code, or administrator-only notes.

## Sandboxed Execution and Grading Goals

All untrusted student code must execute through the sandboxed execution and evaluation architecture. It must not run in frontend JavaScript, `eval`, `child_process`, normal backend request handlers, or unrestricted local runtimes.

The sandboxed execution layer must support Python and JavaScript for the first student-facing implementation, enforce time and resource limits, capture stdout/stderr and runtime errors, clean up execution environments, and return safe execution-result data.

Final submissions must be evaluated against administrator-defined or administrator-approved test cases. Public tests may be used for run feedback; hidden tests must be reserved for submit/grading paths and must remain protected from student-facing APIs and UI.

Submission history, evaluation results, scores, and failure states must be persisted through backend-owned assessment and reporting models.

## Administrator Reporting Goals

Administrators must be able to review assessment outcomes by student, assessment, task, submission status, score or result, and execution details that are safe for administrator views.

AI-disabled reports must show only the Functional Score. AI-enabled reports must
show a `0-100` Functional Score, a separate `0-100` AI Usage Score, and a Final
Score equal to their arithmetic mean.

The AI Usage Score must use Prompt quality and context 30%, Token and
interaction efficiency 40%, Critical evaluation and adaptation 20%, and
Reflection quality and consistency 10%. Token and interaction efficiency must
contain a 30-point structured LLM behavioral assessment and 10 points of
objective repetition metrics.

Reports must include criterion-level grading evidence, reflection and
consistency assessment, total token consumption, number of AI interactions,
average tokens per interaction, per-task token totals, and per-assessment token
totals. Raw token totals are descriptive evidence; grading must not use a fixed
token threshold or cohort-relative token usage.

Reports must remain compatible with per-task grading results and AI telemetry. Student-facing reports must not expose hidden test details or administrator-only notes.

## Embedded AI Agent Goals

The platform must provide an embedded AI agent inside the coding workspace. It should be integrated into the working IDE experience rather than implemented as a separate external chat panel or hint-level system.

The embedded AI agent must be context-aware. It may use task description, selected language, active file content, relevant visible starter files, and student messages. It must not receive hidden tests, hidden expected outputs, grading implementation, administrator-only notes, provider secrets, or system prompts that should remain server-side.

The agent may suggest code edits, explain concepts, assist with debugging, and answer task-related questions. It must guide students instead of dumping direct complete solutions.

The frontend must call only the platform backend AI API. It must never call an external LLM provider directly.

The backend AI service must own provider integration, prompt construction,
safety filtering, structured responses, provider error handling, rate-limit
hooks where applicable, AI telemetry logging, automatic AI usage grading,
rubric versioning, and grading evidence.

For AI-enabled assessments, final submission must require at least one logged AI
interaction. After code is frozen, the student must complete the consolidated
maximum-100-word reflection within a backend-owned ten-minute window. The draft
must autosave and finalize automatically at timeout.

The platform must record suggestion response visibility and student apply,
edit, reject, dismiss, and undo decisions. Applying an actionable suggestion
unchanged within three seconds contributes the bounded rapid-accept deduction
defined in `docs/design/automatic-ai-usage-scoring.md`.

Automatic AI grading must use a fixed versioned rubric and structured
criterion-level evidence. Provider or schema failure must preserve the
Functional Score and result in pending or failed AI grading, not zero.

`anomalyco/opencode` is the reference direction for the embedded coding-agent experience and architecture where feasible. It is an open-source AI coding agent reference, but it must not override project requirements, module boundaries, security rules, or the existing Next.js/.NET/PostgreSQL stack. Use it as inspiration for context-aware coding assistance, agent modes, and controlled tool/context design, not as permission to expose hidden assessment material or collapse frontend/backend/provider boundaries.

Reference: <https://github.com/anomalyco/opencode>

## AI Boundary Contract Goals

The final AI boundary must enforce these rules:

- Module 2 owns the visible embedded agent UI and sends only approved backend API requests.
- Module 4 owns AI provider calls, server-side prompts, response shaping, safety controls, and token accounting.
- Module 1 owns persisted assessment, attempt, task, user, report, and AI-interaction records.
- Module 3 remains responsible for sandboxed execution and must not be bypassed by AI assistance.
- Backend derives user and active attempt context from authentication and assessment scope.
- Provider API keys, system prompts, hidden tests, hidden expected outputs, grading criteria, and administrator-only notes are never sent to the frontend or externalized in student-visible responses.
- Every AI interaction records user, assessment context, task context, interaction type, prompt or message, response, token counts, and timestamp where required by `SPEC.md`.
- AI provider failures must not lose assessment work and must return actionable, safe errors.

## Module Boundary Goals

Module 1, Identity and Assessment Management, owns authentication, RBAC, users, assessments, tasks, test cases, prototype metadata, starter files, attempts, workspace persistence, submissions, results, reports, EF Core, and PostgreSQL-backed state.

Module 2, Interactive Browser-Based Workspace and Frontend IDE, owns the Next.js UI, student and admin pages, workspace shell, file browser, editor, task navigation, run/submit controls, task preview/verification UI, embedded AI UI, frontend API clients, loading states, and error states.

Module 3, Sandboxed Code Execution and Evaluation Engine, owns isolated execution, resource limits, stdout/stderr capture, public and hidden test execution, grading result schemas, execution lifecycle, cleanup, and platform-managed runtime setup.

Module 4, AI Telemetry and Assistance Service, owns secure AI proxying, provider
adapters, server-side prompts, prompt/context safety, structured AI responses,
AI interaction logging, token counting, semantic tags, automatic AI usage
grading, rubric versioning, grading evidence, and AI provider error handling.

Cross-module changes must preserve API contracts, authentication flow, attempt/session ownership, hidden-test protection, sandbox isolation, and frontend/backend separation.

## Security and Non-Goals

The final implementation must not:

- expose hidden test inputs, hidden expected outputs, grading code, or administrator-only notes to students
- allow frontend direct database access
- allow frontend direct sandbox access
- allow frontend direct external AI provider access
- require students to install npm or pip dependencies locally
- require students to clone or run the external Todo prototype locally
- convert the Next.js frontend to Vite
- add `react-router-dom` to the Next.js frontend
- rely on frontend-managed real `session_id` or `attempt_id`
- implement LeetCode-style algorithm tasks as the assessment model
- implement the rejected AI Rescue button
- replace the embedded workspace agent with a separate hint-level chat product
- commit secrets, local credentials, provider keys, JWT secrets, OAuth tokens, or private MCP configuration

## Final Acceptance Gates

The implementation is complete only when the platform satisfies the updated `SPEC.md` and the following final-state checks are true:

- administrators can create flexible manual assessments with practical tasks
- AI-generated full assessment drafts default to the four canonical task categories and remain administrator-reviewable
- the Todo shared prototype is represented as platform-native starter files, metadata, verification modes, and tests
- students can complete all task categories inside the website without local setup
- the workspace supports task switching, multi-file editing, run feedback, submit behavior, and task-specific verification views
- frontend UI extension tasks provide direct browser previews
- REST API, database, and bug-fix tasks provide appropriate output or test-result views
- run uses public checks and submit uses hidden tests without hidden-test leakage
- Python and JavaScript execution are supported through the sandbox architecture
- administrator reports show per-task results and AI usage metrics
- embedded AI assistance is available inside the workspace when enabled
- AI interactions are logged with token counts and assessment/task context
- per-task and per-assessment token totals are reported
- AI-enabled assessments complete the timed reflection workflow
- automatic AI Usage Score follows the 30/40/20/10 rubric
- reports display Functional, AI Usage, and Final scores for AI-enabled attempts
- AI-disabled reports display only the Functional Score
- token metrics remain visible without a fixed or cohort-relative grading threshold
- no module boundary or security constraint is violated

Recommended verification commands for future implementation completion:

```bash
npm run typecheck
npm run build
dotnet build Backend/Backend.sln -v:minimal
dotnet test Backend/Backend.sln -v:minimal
```

If additional checks exist later, especially sandbox, API-contract, browser, or AI telemetry tests, they should be run as part of final acceptance.
