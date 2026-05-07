# Complete Frontend API List and Backend Alignment Checklist

## Status Note

This document is the active frontend/backend API contract and alignment reference.

It includes first frontend-only MVP decisions because the project started with mock UI work. For current backend-connected tasks, do not apply the first-MVP "mock only" rules blindly. Instead:

1. Treat the endpoint names, request/response shapes, error format, security constraints, and session/attempt rules in this document as the intended contract.
2. Inspect the current backend routes, DTOs, frontend API client, and tests before changing integration code.
3. If this document and the implementation disagree, stop and report the mismatch instead of inventing a third contract.
4. Use mock data and `TODO(API)` comments only for frontend-only or not-yet-connected surfaces.
5. Preserve the security boundaries: frontend must never receive hidden test details, call the sandbox directly, call external LLM APIs directly, or create/store/trust/send a real `session_id`.

The architecture PDF remains authoritative for the four-module boundaries and security separation, but some of its endpoint/schema examples are older. For the current backend-connected API contract, this document supersedes older PDF examples such as `/api/v1/sessions/initiate`, `/api/v1/submissions/autosave`, and student-facing `session_id` request fields.

## 0. Purpose

This document supports both UI-first frontend development and current frontend/backend integration.

Frontend plan:
- Build visual UI pages first when a task is frontend-only.
- Use mock data only for frontend-only or not-yet-connected surfaces.
- Put `TODO(API)` comments where real backend calls are intentionally deferred.
- Align endpoint names, request bodies, response bodies, status values, and error formats with backend before coding integration.

Project context:
- Students complete coding assessments in a browser-based IDE.
- Admins create assessments, questions, test cases, and reports.
- Code execution, grading, persistence, authentication, sandbox dispatch, and AI provider access belong to backend.
- Frontend must never receive hidden test cases, call the sandbox directly, or call external LLM APIs directly.
## 0.1 Module 2 MVP Decisions

These decisions remove ambiguity for the first frontend-only MVP. For backend-connected tasks, prefer the current implementation plus the intended contract in this document, and report any drift.

- Frontend stack: prefer Next.js App Router + TypeScript + Tailwind CSS. If an existing app uses another stack, keep the existing stack unless the team explicitly approves migration.
- Routing: use Next.js file-based routes when using Next.js. Do not add `react-router-dom` to a Next.js app.
- Data source for frontend-only work: mock data may be used, and mock objects should match the API response shapes in this document.
- API behavior for backend-connected work: call the real backend through the existing frontend API client. Do not invent endpoints; report mismatches.
- Authentication: mock role selection only for the first visual MVP. Future backend auth should identify the current user from JWT or another secure token; frontend should not manually manage user identity.
- Student languages: Python and JavaScript only. Do not show TypeScript as a student submission language in the first MVP.
- Workspace scope: single-file UI for the first MVP, shaped so it can later evolve into the `files` object contract. Workspace data is scoped by authenticated user + assessment_id + question_id; the backend owns and resolves the active attempt internally.
- Run behavior: mock public/sample test output only. Run does not affect score.
- Submit behavior: mock final result modal. Do not execute hidden tests. Student UI may show hidden test summary counts only, never hidden inputs or expected outputs.
- AI behavior: mock chat, hint, explain, debug, and code-review responses only. Do not call external AI APIs from the frontend. Inline completion is out of scope for the first visual MVP.
- Admin scope: include visual pages for dashboard, assessments, assessment create/edit, question/test-case editing, report list, and report detail. Keep all changes in local mock state only.
- Out of scope for first Module 2 MVP: `/register`, `/admin/users`, backend routes, database logic, sandbox execution, grading engine, real AI provider calls, and report aggregation logic.

---

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
    "code": "ATTEMPT_EXPIRED",
    "message": "The assessment attempt has expired."
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
EXECUTION_FAILED
RATE_LIMITED
INTERNAL_ERROR
```

---

## 2. Auth APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | POST | `/api/v1/auth/login` | Login page |
| P0 | GET | `/api/v1/auth/me` | Protected route guard, current user display |
| P0 | POST | `/api/v1/auth/logout` | Logout button |
| P1 | POST | `/api/v1/auth/register` | Student self-registration, if enabled |
| P2 | POST | `/api/v1/admin/users` | Admin creates/invites users |
| P2 | GET | `/api/v1/admin/users` | Admin user management page |
| P2 | PUT | `/api/v1/admin/users/{user_id}` | Admin updates user role/status |
| P2 | DELETE | `/api/v1/admin/users/{user_id}` | Admin deactivates/deletes user |

### Auth decisions to confirm

- Future backend decision: Auth method, HttpOnly cookie or Bearer token? For Module 2 MVP, use mock role selection only.
- MVP role values for UI: `student`, `administrator`.
- Student self-registration is out of scope for the first Module 2 visual MVP.
- Admin self-registration is out of scope for the MVP. Future backend behavior should not allow public admin self-registration.
- Future backend decision: first admin should be created by seed/setup, not by public registration.
- MVP decision: mock role guard redirects students away from admin pages and administrators away from student pages; real authorization remains backend work.

### Frontend TODO example

```ts
// TODO(API): POST /api/v1/auth/login
// Purpose: authenticate user and redirect by role.
// student -> /student/dashboard
// administrator -> /admin/dashboard
```

---

## 3. Student Dashboard APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | GET | `/api/v1/student/dashboard` | Student dashboard summary cards and recent activity |
| P0 | GET | `/api/v1/student/assessments` | Available/in-progress assessments list |
| P1 | GET | `/api/v1/student/results` | Completed assessments and scores |

### Expected student dashboard data

```json
{
  "summary": {
    "available_assessments": 2,
    "in_progress_attempts": 1,
    "completed_assessments": 3,
    "average_score": 82.5
  },
  "recent_activity": []
}
```

### Student dashboard decisions to confirm

- Should dashboard aggregate data server-side?
- Should assessment cards include `attempt_status`?
- Exact `attempt_status` values:
  - `not_started`
  - `active`
  - `expired`
  - `submitted`
  - `closed`

---

## 4. Admin Dashboard APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | GET | `/api/v1/admin/dashboard` | Admin dashboard metric cards, recent assessments, recent submissions |

### Expected admin dashboard data

```json
{
  "summary": {
    "total_assessments": 12,
    "active_assessments": 3,
    "total_students": 120,
    "total_submissions": 450,
    "average_score": 78.4,
    "ai_interactions": 1320
  },
  "recent_assessments": [],
  "recent_submissions": []
}
```

### Admin dashboard decisions to confirm

- Which metrics are required for the demo?
- Should average score include all assessments or only active/closed assessments?
- Should recent submissions include student names and scores?

---

## 5. Assessment APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | GET | `/api/v1/admin/assessments` | Admin assessment list |
| P1 | POST | `/api/v1/admin/assessments` | Create assessment |
| P1 | GET | `/api/v1/admin/assessments/{assessment_id}` | Admin assessment detail/edit page |
| P1 | PUT | `/api/v1/admin/assessments/{assessment_id}` | Update assessment |
| P2 | POST | `/api/v1/admin/assessments/{assessment_id}/archive` | Archive assessment |
| P2 | DELETE | `/api/v1/admin/assessments/{assessment_id}` | Delete assessment |
| P0 | GET | `/api/v1/assessments/{assessment_id}/context` | Student workspace context for the authenticated user. Backend derives the active attempt from JWT/auth context. |

### Student assessment context must include

```json
{
  "assessment_id": "uuid",
  "title": "Python and JavaScript Coding Assessment",
  "description": "Solve the following coding tasks.",
  "duration_minutes": 60,
  "status": "active",
  "ai_enabled": true,
  "expires_at": "2026-04-30T14:00:00Z",
  "questions": [
    {
      "question_id": "uuid",
      "title": "Array Sum",
      "problem_description_markdown": "## Task\nWrite a function that returns the sum of an array.",
      "language_constraints": ["python", "javascript"],
      "starter_code": {
        "python": "def solve(arr):\n    pass\n",
        "javascript": "function solve(arr) {\n  // TODO\n}\n"
      }
    }
  ]
}
```

### Critical security rule

Student-facing assessment context must never return:
- hidden test cases
- hidden expected outputs
- grading implementation
- admin-only notes

### Assessment decisions to confirm

- MVP decision: one assessment can contain multiple questions; the workspace can display a question list.
- Can questions be reused across assessments?
- Exact assessment status values:
  - `draft`
  - `active`
  - `closed`
  - `archived`
- Can active assessments be edited?
- Is delete hard delete or archive?

---

## 6. Question and Test Case APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P1 | POST | `/api/v1/admin/assessments/{assessment_id}/questions` | Create question |
| P1 | PUT | `/api/v1/admin/questions/{question_id}` | Edit question |
| P2 | DELETE | `/api/v1/admin/questions/{question_id}` | Delete/remove question |
| P1 | GET | `/api/v1/admin/questions/{question_id}/test-cases` | Admin test case list |
| P1 | POST | `/api/v1/admin/questions/{question_id}/test-cases` | Add test cases |
| P2 | PUT | `/api/v1/admin/test-cases/{test_case_id}` | Edit test case |
| P2 | DELETE | `/api/v1/admin/test-cases/{test_case_id}` | Delete test case |

### Question/test case decisions to confirm

- Are problem statements Markdown?
- Are starter codes stored per language?
- MVP supported student languages: `python`, `javascript`.
- TypeScript is not a first-MVP student submission language. It may be considered later.
- Are public test cases visible to students?
- Are hidden test cases visible only to admins?
- What exact input/output format should test cases use?

---

## 7. Assessment Attempt APIs

Frontend must not manually manage, persist, or send `session_id`. The backend identifies the current user from JWT/auth context and resolves the active assessment attempt internally from `user_id + assessment_id`.

### Session/attempt terminology alignment

The architecture PDF uses `session_id` in some schemas to describe the assessment-session identifier. For the current frontend/backend contract, use `attempt` as the product concept. `session_id` may still exist internally in backend/database code, but it is not part of the frontend public API contract.

Canonical rule:
- One authenticated user may have one active attempt for a given assessment in the MVP.
- The backend creates, resumes, validates, and owns that attempt.
- The frontend must not create, store, trust, or send a real `session_id`.
- The frontend should use assessment-scoped APIs where the backend derives the active attempt from auth context.
- The frontend may read returned `attempt_id`/`attempt_status` for display or timer state, but normal workspace/run/submit/AI calls remain assessment-scoped and do not send the attempt id.

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | POST | `/api/v1/assessments/{assessment_id}/attempts/start` | Start or resume the authenticated user's assessment attempt |
| P0 | GET | `/api/v1/assessments/{assessment_id}/attempt` | Get timer and active attempt state for the authenticated user |

### Start/resume attempt response

```json
{
  "attempt_id": "uuid",
  "assessment_id": "uuid",
  "attempt_status": "active",
  "started_at": "2026-04-30T13:00:00Z",
  "expires_at": "2026-04-30T14:00:00Z",
  "server_time": "2026-04-30T13:05:00Z"
}
```

### Attempt decisions

- Frontend does not send or store a real `session_id`; the backend derives the active attempt from auth context.
- The public API no longer exposes session-shaped routes for attempt/workspace/run/submit/AI flows.
- Backend resolves the active attempt from authenticated user + assessment_id.
- Future backend decision: whether one user can have multiple attempts for the same assessment. MVP UI assumes one active attempt at a time and does not expose an attempt identifier.
- Timer source of truth: backend `expires_at` and `server_time`.
- Future backend decision: autosave/run/submit should be rejected after expiry; Module 2 MVP only shows expired/active UI states.
---

## 8. Workspace APIs

Workspace APIs are assessment-scoped. The backend uses JWT/auth context to identify the user and resolve the active attempt. The frontend sends `assessment_id` and question workspace data, but not `session_id` or `attempt_id`.

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | GET | `/api/v1/assessments/{assessment_id}/workspace` | Restore the authenticated user's code after refresh |
| P0 | PUT | `/api/v1/assessments/{assessment_id}/workspace` | Debounced autosave for the authenticated user's workspace |

### Workspace shape

```json
{
  "attempt_id": "uuid",
  "assessment_id": "uuid",
  "questions": {
    "question_uuid_1": {
      "selected_language": "python",
      "active_file": "main.py",
      "files": {
        "main.py": {
          "language": "python",
          "content": "def solve(arr):\n    return sum(arr)\n"
        }
      },
      "last_saved_at": "2026-04-30T13:25:00Z",
      "version": 12
    }
  }
}
```

### Workspace decisions

- MVP decision: single-file UI, with data shaped around the `files` object for future extension.
- Autosave unit: authenticated user + assessment + question.
- MVP decision: autosave indicator simulates a 1000-1500ms debounce after typing stops.
- How to handle version conflicts?
- What should frontend show if autosave fails?
---

## 9. Code Execution APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | POST | `/api/v1/assessments/{assessment_id}/questions/{question_id}/run` | Run current code for public/sample tests |
| P2 | GET | `/api/v1/executions/{execution_id}` | Poll async execution result, if needed |

### Run response shape

Request body:

```json
{
  "selected_language": "python",
  "active_file_content": "def solve(arr):\n    return sum(arr)\n"
}
```

Response body:

```json
{
  "execution_id": "uuid",
  "status": "passed",
  "stdout": "6\n",
  "stderr": null,
  "test_results": [
    {
      "name": "sample test 1",
      "visibility": "public",
      "passed": true,
      "actual_output": "6",
      "expected_output": "6"
    }
  ],
  "metrics": {
    "cpu_time_seconds": 0.04,
    "peak_memory_kb": 12000
  }
}
```

### Execution status values

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

### Execution decisions to confirm

- MVP decision: Run is different from Submit.
- MVP decision: Run uses mocked public/sample tests only.
- MVP decision: Run does not affect score.
- Is execution synchronous or asynchronous?
- MVP can use mocked execution if real sandbox is not ready.

---

## 10. Submission APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | POST | `/api/v1/assessments/{assessment_id}/submit` | Final submit and grading for the authenticated user's active attempt |
| P1 | GET | `/api/v1/assessments/{assessment_id}/submissions?question_id={question_id}` | Authenticated student submission history |
| P1 | GET | `/api/v1/admin/submissions/{submission_id}` | Admin submission detail |

### Final submission response shape

```json
{
  "submission_id": "uuid",
  "evaluation_status": "passed",
  "score": 100,
  "max_score": 100,
  "stdout": "All tests passed.",
  "stderr": null,
  "submitted_at": "2026-04-30T13:40:00Z",
  "visible_test_summary": {
    "passed": 2,
    "failed": 0,
    "total": 2
  },
  "hidden_test_summary": {
    "passed": 8,
    "failed": 0,
    "total": 8
  }
}
```

### Submission decisions to confirm

- Are multiple submissions allowed?
- MVP decision: mock UI presents the latest submission as the counted submission.
- Does submit lock the editor?
- Required decision: hidden test input/output stay hidden from students.
- MVP decision: student-facing frontend may show hidden test summary counts only.

---

## 11. AI APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P1 | POST | `/api/v1/assessments/{assessment_id}/questions/{question_id}/ai/chat` | AI chat/hint/explain/debug/code review |
| P2 | POST | `/api/v1/ai/inline-completion` | Monaco ghost text |
| P1 | GET | `/api/v1/assessments/{assessment_id}/ai-usage` | Authenticated student AI usage summary |
| P2 | GET | `/api/v1/admin/assessments/{assessment_id}/students/{student_id}/ai-interactions` | Admin AI interaction logs for a student's assessment attempt |

### AI chat request

```json
{
  "interaction_type": "hint",
  "message": "Can you give me a small hint?",
  "selected_language": "python",
  "active_file_content": "def solve(arr):\n    pass\n"
}
```

### AI interaction types

```text
chat
hint
explain
debug
code_review
```

### AI decisions to confirm

- MVP decision: AI enabled/disabled is per assessment.
- MVP decision: AI panel supports mocked chat, hint, explain, debug, and code review. Inline completion is out of scope.
- Does backend log every interaction automatically?
- MVP decision: mock AI responses may be displayed as Markdown-style text if the frontend already has a safe renderer; otherwise plain text is acceptable.
- What semantic tags are possible?
- What happens if AI is disabled?
- What happens if AI provider is down?

---

## 12. Report and Analytics APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | GET | `/api/v1/admin/reports` | Admin report list page |
| P0 | GET | `/api/v1/reports/aggregate/{assessment_id}` | Assessment report detail |
| P2 | GET | `/api/v1/admin/reports/{assessment_id}/students/{student_id}` | Student detail report modal |

### Aggregate report should include

```json
{
  "assessment_id": "uuid",
  "assessment_title": "Python Basics",
  "average_score": 82.5,
  "completion_count": 24,
  "participant_count": 28,
  "score_distribution": [
    { "range": "0-20", "count": 1 },
    { "range": "21-40", "count": 2 },
    { "range": "41-60", "count": 4 },
    { "range": "61-80", "count": 7 },
    { "range": "81-100", "count": 10 }
  ],
  "students": [
    {
      "user_id": "uuid",
      "student_name": "Alice Student",
      "student_email": "student@example.com",
      "attempt_status": "submitted",
      "submission_status": "passed",
      "score": 90,
      "max_score": 100,
      "submitted_at": "2026-04-30T13:40:00Z",
      "ai_usage_summary": {
        "total_interactions": 5,
        "main_semantic_tags": ["conceptual_hint", "debug"]
      }
    }
  ]
}
```

### Report decisions to confirm

- Which metrics are needed for admin reports?
- Should reports include raw AI logs or only summaries?
- Should report generation be live or cached?
- Should hidden test details be visible to admins in reports?
- What chart data should backend provide?

---

## 13. System APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P2 | GET | `/api/v1/health` | Health check |
| P0 | GET | `/api/v1/config` | Feature flags and supported languages |

### Config response

```json
{
  "features": {
    "registration_enabled": true,
    "ai_chat_enabled": true,
    "ai_inline_completion_enabled": false,
    "multi_file_workspace_enabled": false,
    "real_sandbox_enabled": false
  },
  "supported_languages": ["python", "javascript"]
}
```

---

## 14. Frontend Page to API Mapping

### Public Pages

| Page | APIs |
|---|---|
| `/login` | `POST /api/v1/auth/login` |
| `/register` | `POST /api/v1/auth/register` |
| Protected layout | `GET /api/v1/auth/me` |
| Logout button | `POST /api/v1/auth/logout` |

### Student Pages

| Page | APIs |
|---|---|
| `/student/dashboard` | `GET /api/v1/student/dashboard`, `GET /api/v1/student/assessments`, `GET /api/v1/student/results` |
| `/student/assessments` | `GET /api/v1/student/assessments` |
| `/student/results` | `GET /api/v1/student/results` |
| `/student/assessments/{assessment_id}/start` | `POST /api/v1/assessments/{assessment_id}/attempts/start` |
| `/student/assessments/{assessment_id}/workspace` | `GET /api/v1/assessments/{assessment_id}/context`, `GET /api/v1/assessments/{assessment_id}/attempt`, `GET /api/v1/assessments/{assessment_id}/workspace`, `PUT /api/v1/assessments/{assessment_id}/workspace`, `POST /api/v1/assessments/{assessment_id}/questions/{question_id}/run`, `POST /api/v1/assessments/{assessment_id}/submit`, `POST /api/v1/assessments/{assessment_id}/questions/{question_id}/ai/chat` |

### Admin Pages

| Page | APIs |
|---|---|
| `/admin/dashboard` | `GET /api/v1/admin/dashboard` |
| `/admin/assessments` | `GET /api/v1/admin/assessments` |
| `/admin/assessments/new` | `POST /api/v1/admin/assessments` |
| `/admin/assessments/{assessment_id}` | `GET /api/v1/admin/assessments/{assessment_id}`, `PUT /api/v1/admin/assessments/{assessment_id}` |
| `/admin/assessments/{assessment_id}/questions/new` | `POST /api/v1/admin/assessments/{assessment_id}/questions` |
| `/admin/questions/{question_id}` | `PUT /api/v1/admin/questions/{question_id}`, `GET /api/v1/admin/questions/{question_id}/test-cases` |
| `/admin/reports` | `GET /api/v1/admin/reports` |
| `/admin/reports/{assessment_id}` | `GET /api/v1/reports/aggregate/{assessment_id}` |
| Student report detail modal | `GET /api/v1/admin/reports/{assessment_id}/students/{student_id}` |
| Submission detail modal | `GET /api/v1/admin/submissions/{submission_id}` |
| `/admin/users` | `GET /api/v1/admin/users`, `POST /api/v1/admin/users` |

---

## 15. Priority Summary

### P0: Required for first visual demo

```text
GET  /api/v1/config
POST /api/v1/auth/login
GET  /api/v1/auth/me
POST /api/v1/auth/logout

GET  /api/v1/student/dashboard
GET  /api/v1/student/assessments
POST /api/v1/assessments/{assessment_id}/attempts/start
GET  /api/v1/assessments/{assessment_id}/context
GET  /api/v1/assessments/{assessment_id}/attempt
GET  /api/v1/assessments/{assessment_id}/workspace
PUT  /api/v1/assessments/{assessment_id}/workspace
POST /api/v1/assessments/{assessment_id}/questions/{question_id}/run
POST /api/v1/assessments/{assessment_id}/submit

GET  /api/v1/admin/dashboard
GET  /api/v1/admin/assessments
GET  /api/v1/admin/reports
GET  /api/v1/reports/aggregate/{assessment_id}
```

### P1: Needed for complete MVP

```text
POST /api/v1/auth/register
GET  /api/v1/student/results

POST /api/v1/admin/assessments
GET  /api/v1/admin/assessments/{assessment_id}
PUT  /api/v1/admin/assessments/{assessment_id}
POST /api/v1/admin/assessments/{assessment_id}/questions
PUT  /api/v1/admin/questions/{question_id}
GET  /api/v1/admin/questions/{question_id}/test-cases
POST /api/v1/admin/questions/{question_id}/test-cases

GET  /api/v1/assessments/{assessment_id}/submissions
POST /api/v1/assessments/{assessment_id}/questions/{question_id}/ai/chat
GET  /api/v1/assessments/{assessment_id}/ai-usage
GET  /api/v1/admin/submissions/{submission_id}
```

### P2: Advanced features

```text
POST /api/v1/ai/inline-completion
GET  /api/v1/admin/assessments/{assessment_id}/students/{student_id}/ai-interactions
GET  /api/v1/admin/reports/{assessment_id}/students/{student_id}

POST /api/v1/admin/users
GET  /api/v1/admin/users
PUT  /api/v1/admin/users/{user_id}
DELETE /api/v1/admin/users/{user_id}

POST   /api/v1/admin/assessments/{assessment_id}/archive
DELETE /api/v1/admin/assessments/{assessment_id}
DELETE /api/v1/admin/questions/{question_id}
PUT    /api/v1/admin/test-cases/{test_case_id}
DELETE /api/v1/admin/test-cases/{test_case_id}
GET    /api/v1/executions/{execution_id}
```

---

## 16. Recommended Frontend TODO Comment Style

```ts
// TODO(API): GET /api/v1/admin/dashboard
// Purpose: Load admin dashboard summary cards and recent activity.
// Replace mock data when backend endpoint is ready.
```

```ts
// TODO(API): POST /api/v1/assessments/{assessment_id}/questions/{question_id}/run
// Purpose: Send current code, language, assessment_id, and question_id. Backend derives user and active attempt from auth context.
// Expected response: status, stdout, stderr, test_results, metrics.
```

```ts
// TODO(API): PUT /api/v1/assessments/{assessment_id}/workspace
// Purpose: Debounced autosave for Monaco editor content.
// Save unit: authenticated user + assessment + question.
// Debounce: 1000-1500ms after typing stops.
```

---

## 17. Recommended Mock Data Files

```text
src/mocks/auth.mock.ts
src/mocks/student-dashboard.mock.ts
src/mocks/admin-dashboard.mock.ts
src/mocks/assessments.mock.ts
src/mocks/workspace.mock.ts
src/mocks/reports.mock.ts
src/mocks/ai.mock.ts
```

Mock data should match the planned real API response shapes.

---

## 18. Message to Backend Engineer

```text
I am preparing the frontend UI first. I will build visual pages with mock data and leave TODO(API) comments where backend calls will be inserted later.

To avoid mismatches later, I want to align the full API surface early.

The frontend needs API coverage for:
1. Auth
2. Student dashboard
3. Admin dashboard
4. Assessment management
5. Question and test case management
6. Assessment attempt management
7. Workspace restore/autosave
8. Code execution
9. Final submissions
10. AI assistance and AI usage tracking
11. Reports and analytics
12. User management
13. System config/feature flags

Important boundaries:
- frontend must never receive hidden test case details
- frontend must not call sandbox directly
- frontend must not call external LLM APIs directly
- backend owns grading, persistence, auth/JWT identity, role authorization, active attempt resolution, sandbox dispatch, and AI provider access

Please confirm:
- final endpoint names
- request/response shapes
- role values
- status enums
- auth method, including whether JWT is used and how active attempts are resolved
- error format
- dashboard metrics
- whether Run and Submit are separate
- whether latest or best submission counts
- whether MVP supports single-file or multi-file workspace
- which AI features are P1/P2
```





