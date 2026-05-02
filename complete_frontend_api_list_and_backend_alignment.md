# Complete Frontend API List and Backend Alignment Checklist

## 0. Purpose

This document is for UI-first frontend development.

Frontend plan:
- Build visual UI pages first.
- Use mock data first.
- Put `TODO(API)` comments where real backend calls will be added later.
- Align endpoint names, request bodies, response bodies, status values, and error formats with backend before coding integration.

Project context:
- Students complete coding assessments in a browser-based IDE.
- Admins create assessments, questions, test cases, and reports.
- Code execution, grading, persistence, authentication, sandbox dispatch, and AI provider access belong to backend.
- Frontend must never receive hidden test cases, call the sandbox directly, or call external LLM APIs directly.

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
    "code": "SESSION_EXPIRED",
    "message": "The assessment session has expired."
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
SESSION_NOT_FOUND
SESSION_EXPIRED
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

- Auth method: HttpOnly cookie or Bearer token?
- Exact role values: `student`, `administrator`?
- Can students self-register?
- Can admins self-register? Recommended: no.
- How is the first admin created? Recommended: seeded super admin.
- What happens when a user accesses a page outside their role?

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
    "in_progress_sessions": 1,
    "completed_assessments": 3,
    "average_score": 82.5
  },
  "recent_activity": []
}
```

### Student dashboard decisions to confirm

- Should dashboard aggregate data server-side?
- Should assessment cards include `session_status`?
- Exact `session_status` values:
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
| P0 | GET | `/api/v1/assessments/{assessment_id}/context?session_id={session_id}` | Student workspace context |

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

- Can one assessment contain multiple questions? Recommended: yes.
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
- MVP supported student languages: recommended `python`, `javascript`.
- Is TypeScript a student submission language now or later?
- Are public test cases visible to students?
- Are hidden test cases visible only to admins?
- What exact input/output format should test cases use?

---

## 7. Session APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | POST | `/api/v1/sessions/initiate` | Start or resume assessment session |
| P0 | GET | `/api/v1/sessions/{session_id}` | Get timer/session state |
| P2 | POST | `/api/v1/sessions/{session_id}/complete` | End session manually, optional |

### Session initiate response

```json
{
  "session_id": "uuid",
  "assessment_id": "uuid",
  "session_status": "active",
  "started_at": "2026-04-30T13:00:00Z",
  "expires_at": "2026-04-30T14:00:00Z",
  "server_time": "2026-04-30T13:05:00Z"
}
```

### Session decisions to confirm

- Does `/sessions/initiate` create a new session or resume an active one?
- Can one student have multiple active sessions for the same assessment? Recommended: no.
- Timer source of truth: backend `expires_at` and `server_time`.
- What happens after expiry?
- Should autosave/run/submit be rejected after expiry? Recommended: yes.

---

## 8. Workspace APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | GET | `/api/v1/sessions/{session_id}/workspace` | Restore code after refresh |
| P0 | PUT | `/api/v1/sessions/{session_id}/workspace` | Debounced autosave |

### Workspace shape

```json
{
  "session_id": "uuid",
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

### Workspace decisions to confirm

- MVP single-file or multi-file? Recommended: single-file UI, `files` object for future extension.
- Autosave unit: session + question.
- Debounce: 1000–1500ms after typing stops.
- How to handle version conflicts?
- What should frontend show if autosave fails?

---

## 9. Code Execution APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | POST | `/api/v1/executions/run` | Run current code |
| P2 | GET | `/api/v1/executions/{execution_id}` | Poll async execution result, if needed |

### Run response shape

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

- Is Run different from Submit? Recommended: yes.
- Does Run use public/sample tests only? Recommended: yes.
- Does Run affect score? Recommended: no.
- Is execution synchronous or asynchronous?
- MVP can use mocked execution if real sandbox is not ready.

---

## 10. Submission APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P0 | POST | `/api/v1/submissions/finalize` | Final submit and grading |
| P1 | GET | `/api/v1/sessions/{session_id}/submissions?question_id={question_id}` | Student submission history |
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
- Does latest submission or best submission count? Recommended MVP: latest submission counts.
- Does submit lock the editor?
- Should hidden test input/output stay hidden? Required: yes.
- Should frontend show hidden test summary only? Recommended: yes.

---

## 11. AI APIs

| Priority | Method | Endpoint | Frontend Use |
|---|---|---|---|
| P1 | POST | `/api/v1/ai/chat` | AI chat/hint/explain/debug/code review |
| P2 | POST | `/api/v1/ai/inline-completion` | Monaco ghost text |
| P1 | GET | `/api/v1/sessions/{session_id}/ai-usage` | AI usage summary |
| P2 | GET | `/api/v1/admin/sessions/{session_id}/ai-interactions` | Admin AI interaction logs |

### AI chat request

```json
{
  "session_id": "uuid",
  "assessment_id": "uuid",
  "question_id": "uuid",
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

- Is AI enabled per assessment or per question? Recommended MVP: per assessment.
- Which AI features are MVP?
  - P1: chat/hint/debug
  - P2: inline completion
- Does backend log every interaction automatically?
- Should AI response be Markdown? Recommended: yes.
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
      "session_status": "submitted",
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
| `/student/assessments/{assessment_id}/start` | `POST /api/v1/sessions/initiate` |
| `/student/assessments/{assessment_id}/workspace` | `GET /api/v1/assessments/{assessment_id}/context`, `GET /api/v1/sessions/{session_id}`, `GET /api/v1/sessions/{session_id}/workspace`, `PUT /api/v1/sessions/{session_id}/workspace`, `POST /api/v1/executions/run`, `POST /api/v1/submissions/finalize`, `POST /api/v1/ai/chat` |

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
POST /api/v1/sessions/initiate
GET  /api/v1/assessments/{assessment_id}/context?session_id={session_id}
GET  /api/v1/sessions/{session_id}
GET  /api/v1/sessions/{session_id}/workspace
PUT  /api/v1/sessions/{session_id}/workspace
POST /api/v1/executions/run
POST /api/v1/submissions/finalize

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

GET  /api/v1/sessions/{session_id}/submissions
POST /api/v1/ai/chat
GET  /api/v1/sessions/{session_id}/ai-usage
GET  /api/v1/admin/submissions/{submission_id}
```

### P2: Advanced features

```text
POST /api/v1/ai/inline-completion
GET  /api/v1/admin/sessions/{session_id}/ai-interactions
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
// TODO(API): POST /api/v1/executions/run
// Purpose: Send current code, language, assessment_id, question_id, session_id.
// Expected response: status, stdout, stderr, test_results, metrics.
```

```ts
// TODO(API): PUT /api/v1/sessions/{session_id}/workspace
// Purpose: Debounced autosave for Monaco editor content.
// Save unit: session + question.
// Debounce: 1000–1500ms after typing stops.
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
6. Session management
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
- backend owns grading, persistence, auth, role authorization, sandbox dispatch, and AI provider access

Please confirm:
- final endpoint names
- request/response shapes
- role values
- status enums
- auth method
- error format
- dashboard metrics
- whether Run and Submit are separate
- whether latest or best submission counts
- whether MVP supports single-file or multi-file workspace
- which AI features are P1/P2
```
