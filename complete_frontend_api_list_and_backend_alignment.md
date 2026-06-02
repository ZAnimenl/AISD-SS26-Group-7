# Complete Frontend API List and Backend Alignment Checklist

## Status Note

This document is the active frontend/backend API contract and alignment reference for the updated requirements in `SPEC.md`.

The updated feature set keeps the existing backend-owned attempt model and adds structured AI assistance, per-question AI credits, AI Rescue, LLM-based task generation, post-submission reflection, process-aware scoring, and controlled student report release.

Current implementation may still expose only part of this contract. When implementation and this document disagree, inspect the current backend routes, DTOs, frontend API client, and tests before coding. Report the mismatch rather than inventing a third contract.

Preserve these security boundaries:

- Frontend must never receive hidden test details, grading implementation, AI Rescue correctness labels during an assessment, provider secrets, or other students' reports.
- Frontend must never call the sandbox or external LLM providers directly.
- Frontend must not create, store, trust, or send a real `session_id`; public assessment flows are assessment-scoped and backend-owned.
- During assessments, AI assistance must use configured structured modes. Unrestricted free-form AI chat is not allowed.

## Current Implementation Baseline

| Area | Current status |
|---|---|
| Frontend | Next.js App Router, TypeScript, Tailwind, Monaco workspace, student/admin pages |
| Backend | ASP.NET API under `/api/v1` |
| Database | PostgreSQL through EF Core |
| Auth | Bearer token stored by frontend API client; backend resolves current user from auth context |
| Attempts | Backend-owned assessment attempts resolved from authenticated user + assessment |
| Workspace | Backend-backed restore/autosave, assessment-scoped public APIs |
| Languages | Python, JavaScript, and TypeScript |
| Execution | Backend-controlled Docker grading pipeline |
| AI | Existing backend AI endpoint/logging; updated contract requires structured hints, credits, Rescue, task generation, and reflection evaluation |
| Reports | Existing admin reports; updated contract requires process-aware scoring and release gates |

## 1. Standard API Contract

### Success response

```json
{
  "ok": true,
  "data": {}
}
```

### Error response

```json
{
  "ok": false,
  "error": {
    "code": "AI_CREDITS_EXHAUSTED",
    "message": "No AI credits remain for this question."
  }
}
```

### Common error codes

```text
UNAUTHENTICATED
FORBIDDEN
VALIDATION_ERROR
NOT_FOUND
ASSESSMENT_NOT_FOUND
QUESTION_NOT_FOUND
ATTEMPT_NOT_FOUND
ATTEMPT_EXPIRED
ASSESSMENT_CLOSED
AI_DISABLED
AI_MODE_DISABLED
AI_CREDITS_EXHAUSTED
AI_RESCUE_EXHAUSTED
REFLECTION_REQUIRED
REFLECTION_EXPIRED
REPORT_NOT_RELEASED
EXECUTION_FAILED
PROVIDER_UNAVAILABLE
INTERNAL_ERROR
```

## 2. Auth APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | POST | `/api/v1/auth/login` | Login page |
| P0 | GET | `/api/v1/auth/me` | Protected route guard and current user display |
| P0 | POST | `/api/v1/auth/logout` | Logout button |
| P1 | POST | `/api/v1/auth/register` | Student self-registration, if enabled |
| P2 | GET | `/api/v1/admin/users` | Admin user management |
| P2 | POST | `/api/v1/admin/users` | Admin creates/invites users |
| P2 | PUT | `/api/v1/admin/users/{user_id}` | Admin updates role/status |
| P2 | DELETE | `/api/v1/admin/users/{user_id}` | Admin deactivates/deletes users |

Role values: `student`, `administrator`.

## 3. Student APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | GET | `/api/v1/student/dashboard` | Student dashboard summary cards |
| P0 | GET | `/api/v1/student/assessments` | Available/in-progress assessments |
| P1 | GET | `/api/v1/student/results` | Completed/released results list |
| P1 | GET | `/api/v1/student/results/{assessment_id}` | Released result detail |

Student result endpoints must return `REPORT_NOT_RELEASED` when the administrator has not released final reports for that assessment.

## 4. Admin Dashboard APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | GET | `/api/v1/admin/dashboard` | Admin metric cards, recent assessments, recent submissions |

Updated dashboard metrics should include code correctness, completion counts, AI hint usage, AI credit usage, Rescue usage, reflection completion, and report-release status where available.

## 5. Assessment APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | GET | `/api/v1/admin/assessments` | Admin assessment list |
| P1 | POST | `/api/v1/admin/assessments` | Create assessment |
| P1 | GET | `/api/v1/admin/assessments/{assessment_id}` | Admin assessment detail/edit page |
| P1 | PUT | `/api/v1/admin/assessments/{assessment_id}` | Update assessment and AI feature settings |
| P2 | POST | `/api/v1/admin/assessments/{assessment_id}/archive` | Archive assessment |
| P2 | DELETE | `/api/v1/admin/assessments/{assessment_id}` | Delete assessment |
| P0 | GET | `/api/v1/assessments/{assessment_id}/context` | Student workspace context |
| P1 | POST | `/api/v1/admin/assessments/{assessment_id}/reports/release` | Release student reports |
| P1 | POST | `/api/v1/admin/assessments/{assessment_id}/reports/unrelease` | Hide student reports again if policy allows |

### Assessment fields

```json
{
  "assessment_id": "uuid",
  "title": "Python and JavaScript Coding Assessment",
  "description": "Solve the following coding tasks.",
  "duration_minutes": 60,
  "status": "active",
  "ai_enabled": true,
  "ai_settings": {
    "structured_hints_enabled": true,
    "ai_credits_enabled": true,
    "ai_rescue_enabled": true,
    "reflection_enabled": true,
    "rescue_correctness_probability": 0.5
  },
  "reports_released": false
}
```

Status values: `draft`, `active`, `closed`, `archived`.

Student-facing context may include enabled feature flags and remaining AI budgets, but not hidden tests, grading implementation, admin notes, or Rescue correctness labels.

## 6. Question and Test Case APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P1 | POST | `/api/v1/admin/assessments/{assessment_id}/questions` | Create question |
| P1 | PUT | `/api/v1/admin/questions/{question_id}` | Edit question, difficulty, starter code, credit override |
| P2 | DELETE | `/api/v1/admin/questions/{question_id}` | Delete/remove question |
| P1 | GET | `/api/v1/admin/questions/{question_id}/test-cases` | Admin test case list |
| P1 | POST | `/api/v1/admin/questions/{question_id}/test-cases` | Add test cases |
| P2 | PUT | `/api/v1/admin/test-cases/{test_case_id}` | Edit test case |
| P2 | DELETE | `/api/v1/admin/test-cases/{test_case_id}` | Delete test case |

Question fields should include title, problem description, supported languages, difficulty, grading configuration, starter code, and optional AI credit budget override.

Default AI credits by difficulty:

| Difficulty | Credits |
|---|---:|
| easy | 6 |
| medium | 10 |
| hard | 15 |

## 7. LLM-Based Task Generation APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P1 | POST | `/api/v1/admin/task-generation/drafts` | Generate draft coding task |
| P1 | GET | `/api/v1/admin/task-generation/drafts/{draft_id}` | Review generated draft |
| P1 | PUT | `/api/v1/admin/task-generation/drafts/{draft_id}` | Edit generated draft |
| P1 | POST | `/api/v1/admin/task-generation/drafts/{draft_id}/regenerate` | Regenerate draft content |
| P1 | POST | `/api/v1/admin/task-generation/drafts/{draft_id}/accept` | Add reviewed draft to an assessment |
| P1 | DELETE | `/api/v1/admin/task-generation/drafts/{draft_id}` | Reject generated draft |

Generation request:

```json
{
  "assessment_id": "uuid",
  "topic": "arrays and hashing",
  "language": "python",
  "difficulty": "medium",
  "expected_duration_minutes": 20,
  "desired_test_case_count": 6
}
```

Generated tasks remain draft content until an administrator reviews test cases and marks them public or hidden.

## 8. Assessment Attempt APIs

Frontend must not manually manage, persist, or send `session_id`. The backend identifies the current user from auth context and resolves the active assessment attempt internally from `user_id + assessment_id`.

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | POST | `/api/v1/assessments/{assessment_id}/attempts/start` | Start or resume attempt |
| P0 | GET | `/api/v1/assessments/{assessment_id}/attempt` | Timer, attempt state, remaining Rescue chances |

Attempt response may include `attempt_id` for display/debugging, but caller-owned attempt IDs are not used by workspace/run/submit/AI/reflection APIs.

## 9. Workspace APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | GET | `/api/v1/assessments/{assessment_id}/workspace` | Restore authenticated user's code |
| P0 | PUT | `/api/v1/assessments/{assessment_id}/workspace` | Debounced autosave |

Autosave unit: authenticated user + assessment + question. Workspace calls do not send `session_id` or `attempt_id`.

## 10. Code Execution APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | POST | `/api/v1/assessments/{assessment_id}/questions/{question_id}/run` | Run current code against public/sample tests |
| P2 | GET | `/api/v1/executions/{execution_id}` | Poll async result if needed |

Execution status values:

```text
queued
running
passed
failed
runtime_error
time_limit_exceeded
memory_limit_exceeded
internal_error
```

Run is different from Submit. Run uses public/sample tests and does not affect final score. Submit may use hidden tests, but hidden inputs/outputs stay hidden from students.

## 11. Submission and Reflection APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | POST | `/api/v1/assessments/{assessment_id}/submit` | Freeze code and start final grading/reflection flow |
| P1 | GET | `/api/v1/assessments/{assessment_id}/submissions?question_id={question_id}` | Authenticated student submission history |
| P1 | GET | `/api/v1/admin/submissions/{submission_id}` | Admin submission detail |
| P1 | GET | `/api/v1/assessments/{assessment_id}/reflection` | Get reflection state after submit |
| P1 | POST | `/api/v1/assessments/{assessment_id}/reflection` | Submit reflection text |
| P1 | POST | `/api/v1/assessments/{assessment_id}/reflection/auto-submit` | Backend/admin timer completion hook if needed |

Submit behavior:

- When reflection is disabled, final submit can mark the attempt complete after grading.
- When reflection is enabled, submit freezes code for grading and opens a timed reflection form.
- Reflection asks the student to explain code, AI usage, and whether they trusted, rejected, or modified AI-generated suggestions.
- Reflection time limit is 5 minutes.
- If time expires, the backend stores any entered text and completes the attempt.

## 12. Structured AI APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P1 | GET | `/api/v1/assessments/{assessment_id}/ai-state` | Remaining credits, Rescue chances, enabled modes |
| P1 | POST | `/api/v1/assessments/{assessment_id}/questions/{question_id}/ai/hints` | Request structured normal AI hint |
| P1 | POST | `/api/v1/assessments/{assessment_id}/questions/{question_id}/ai/rescue` | Request AI Rescue suggestion |
| P1 | POST | `/api/v1/assessments/{assessment_id}/questions/{question_id}/ai/rescue/{rescue_id}/decision` | Accept/reject/modify Rescue suggestion |
| P1 | GET | `/api/v1/assessments/{assessment_id}/ai-usage` | Authenticated student AI usage summary |
| P1 | GET | `/api/v1/admin/assessments/{assessment_id}/students/{student_id}/ai-interactions` | Admin AI interaction details |

Structured hint request:

```json
{
  "hint_level": "debugging_hint",
  "message": "My loop fails on empty arrays.",
  "selected_language": "python",
  "active_file_content": "def solve(arr):\n    ..."
}
```

Hint levels:

```text
concept_hint
strategy_hint
debugging_hint
pseudocode_hint
code_level_suggestion
```

Suggested default credit costs:

| Hint level | Cost |
|---|---:|
| concept_hint | 1 |
| strategy_hint | 2 |
| debugging_hint | 3 |
| pseudocode_hint | 4 |
| code_level_suggestion | 6 |

AI Rescue rules:

- 4 Rescue chances per assessment, shared across questions.
- Rescue does not consume normal AI credits.
- Each Rescue suggestion is generated as correct or misleading according to assessment-level probability, default `0.5`.
- Rescue correctness labels are hidden from students during the assessment and visible only to authorized admin reports.
- Student decision values: `accepted`, `rejected`, `modified`.
- Decision time should be logged.

## 13. Report, Scoring, and Analytics APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | GET | `/api/v1/admin/reports` | Admin report list page |
| P0 | GET | `/api/v1/reports/aggregate/{assessment_id}` | Assessment report detail |
| P1 | GET | `/api/v1/admin/reports/{assessment_id}/students/{student_id}` | Admin student detail report |
| P1 | POST | `/api/v1/admin/reports/{assessment_id}/students/{student_id}/process-evaluation` | Generate/retry LLM process evaluation |
| P1 | GET | `/api/v1/student/results/{assessment_id}` | Released student report |

Admin reports should include student identifier, assessment identifier, submission status, code correctness score, test results, AI hint usage, AI credit usage, Rescue behavior, AI interaction summary, reflection, LLM-based process evaluation, process-aware score, and component explanations.

Process-aware score weights:

| Condition | Code correctness | AI usage quality | Reflection understanding | Critical AI judgment |
|---|---:|---:|---:|---:|
| Student used AI Rescue | 60% | 15% | 15% | 10% |
| Student did not use AI Rescue | 70% | 15% | 15% | 0% |

LLM evaluation is decision support only and must not be presented as an autonomous final grading or hiring decision.

Released student reports may include final scores, LLM evaluations, and short explanations. They must not expose hidden test details, Rescue correctness labels for other students, or full admin-only logs.

## 14. System APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P2 | GET | `/api/v1/health` | Health check |
| P0 | GET | `/api/v1/config` | Feature flags and supported languages |

Config response should include supported languages, auth method, roles, and feature flags for structured hints, AI credits, AI Rescue, task generation, reflection, process scoring, report release, and inline completion if ever enabled.

## 15. Frontend Page to API Mapping

| Page | APIs |
|---|---|
| `/login` | `POST /api/v1/auth/login` |
| `/register` | `POST /api/v1/auth/register` |
| Protected layout | `GET /api/v1/auth/me` |
| `/student/dashboard` | `GET /api/v1/student/dashboard`, `GET /api/v1/student/assessments`, `GET /api/v1/student/results` |
| `/student/assessments` | `GET /api/v1/student/assessments` |
| `/student/results` | `GET /api/v1/student/results`, `GET /api/v1/student/results/{assessment_id}` |
| `/student/assessments/{assessment_id}/start` | `POST /api/v1/assessments/{assessment_id}/attempts/start` |
| `/student/assessments/{assessment_id}/workspace` | context, attempt, workspace, run, submit, AI state, hints, Rescue, reflection |
| `/admin/dashboard` | `GET /api/v1/admin/dashboard` |
| `/admin/assessments` | assessment list/create/update/archive/delete |
| `/admin/assessments/{assessment_id}` | assessment settings, questions, test cases, AI feature configuration |
| Task generation modal/page | task-generation draft APIs |
| `/admin/reports` | `GET /api/v1/admin/reports` |
| `/admin/reports/{assessment_id}` | aggregate report, release/unrelease controls |
| Student report detail modal | admin student detail report, AI interaction logs, process evaluation |
| `/admin/users` | admin user APIs |

## 16. Priority Summary for Implementation

### P0: Preserve and stabilize the existing connected baseline

```text
Auth, assessment context, backend-owned attempts, workspace restore/autosave,
run, submit, code-correctness grading, admin/student dashboards, base reports.
```

### P1: Implement the updated five-feature package in dependency order

```text
Assessment/question AI settings and difficulty/credit fields
Structured hint levels + AI credit budget
AI Rescue + decision logging
Post-submission reflection
Process-aware reports + controlled student result release
LLM task generation with admin review
```

### P2: Polish and expansion

```text
Inline completion, async execution polling, richer report charts, cached report generation,
multiple attempts per assessment, multi-file workspace, advanced provider adapters.
```

## 17. Recommended Implementation Split

Do not implement all updated features as one undifferentiated change. They share data, but the risk is much lower if they are split into staged increments:

1. Data model and contracts: feature flags, difficulty, credit budget, Rescue settings, report-release flag, DTOs.
2. Structured AI credits: replace generic assessment chat with hint-level requests, cost deduction, remaining-credit UI, logging.
3. AI Rescue: separate Rescue budget, correct/misleading generation labels, student decision UI, admin-only logging.
4. Submission reflection: freeze submitted code, collect 5-minute reflection, store reflection, handle timeout.
5. Reporting and release: process-aware score breakdown, LLM evaluation summaries, admin release gate, student result visibility.
6. LLM task generation: admin draft generation/review flow. This can be built after the assessment runtime features because it mostly affects authoring.

Stages 2 and 3 can share Module 4 plumbing, but they should still be separate commits/tasks. Stages 4 and 5 should follow after the runtime logs exist.
