# Module 2 Frontend UI Task
## Interactive Browser-Based Workspace / Frontend IDE

## 0. My Assigned Module

I am responsible for **Module 2: Interactive Browser-Based Workspace / Frontend IDE**.

This module is the visual and interactive frontend layer of the AI-assisted online coding assessment platform.

It includes:

- Student-facing browser-based coding workspace
- Monaco Editor integration
- Problem statement display
- Language selector
- Output/console area
- Run and Submit button UI
- Autosave UI behavior
- AI assistant panel UI
- Student dashboard UI
- Admin dashboard, assessment list, assessment create/edit UI, question/test-case UI, report list, and report detail UI for the frontend demo only

It does **not** include:

- Real authentication backend or JWT handling
- Database implementation
- Real sandboxed code execution
- Real AI provider calls
- Grading engine
- Hidden test case management
- Backend report aggregation logic

---

## 1. Source Documents

The AI Agent must read and follow these files:

1. `requirements.md`
   - Source of product requirements and MVP constraints.

2. `complete_frontend_api_list_and_backend_alignment.md`
   - Source of future API contracts.
   - For now, use these APIs only as `TODO(API)` comments.

3. `ui-style-reference.md`
   - Source of visual style inspiration.
   - It must not override `requirements.md`.

4. `Architectural Design and Module Specification for an AI-Assisted Online Coding Assessment Platform.pdf`
   - Optional reference if available in the repository.
   - If the PDF is not present, use this file plus `requirements.md` as the Module 2 authority.

---

## 2. Current Implementation Goal

Build the **frontend visual UI first**.

The goal is to create a demo-ready frontend where users can click through the main flows using mock data.

Real backend integration will happen later.

---

## 3. Strict Constraints

The AI Agent must obey these constraints:

- Do not implement real backend logic.
- Do not implement database logic.
- Do not implement real authentication backend.
- Do not implement real code execution.
- Do not use `eval`.
- Do not use `child_process`.
- Do not use Docker.
- Do not use local Python, Node.js, or other runtimes to execute student submissions.
- Do not call external AI APIs.
- Do not expose hidden test cases in student-facing UI.
- Do not modify backend, database schema, sandbox, or AI provider logic.
- Frontend MVP must not manage a real `session_id`. In a mock-only flow, use mock active assessment attempt state if needed. In a backend-connected flow, keep any backend-returned attempt/session-shaped identifier in memory only as a transient compatibility value.
- Use `TODO(API)` comments where backend calls will be inserted later.

Framework decision for this project:

- Preferred frontend stack: Next.js with the App Router, TypeScript, Tailwind CSS, shadcn/ui-compatible components, lucide-react icons, and optional Framer Motion.
- If a Next.js app exists or is created, do not convert it to Vite.
- If a Next.js app exists or is created, do not add `react-router-dom`; use file-based routing under `src/app`.
- If the repository later already contains a different frontend stack, keep that existing stack and do not migrate frameworks without explicit approval.

---

## 4. MVP Student Languages

For the first MVP, the student coding workspace supports:

- Python
- JavaScript

Do not show TypeScript as a student submission language in the MVP UI.

TypeScript may be used for the project codebase itself.

---

## 5. Required Frontend Pages

### Public Pages

- `/login`
- Do not build `/register` in the first Module 2 MVP unless the team explicitly adds student self-registration later.

### Student Pages

- `/student/dashboard`
- `/student/assessments`
- `/student/results`
- `/student/assessments/[assessmentId]/start`
- `/student/assessments/[assessmentId]/workspace`

### Admin Pages

- `/admin/dashboard`
- `/admin/assessments`
- `/admin/assessments/new`
- `/admin/assessments/[assessmentId]`
- `/admin/reports`
- `/admin/reports/[assessmentId]`
- Do not build `/admin/users` in the first Module 2 MVP unless the team explicitly adds user management later.

---

## 6. Required Student Workspace UI

The student workspace must look like a browser-based coding IDE.

It should include:

### Top Bar

- Assessment title
- Timer
- Autosave status
- Submit button

### Left Panel

- Question list
- Problem statement
- Examples or constraints

### Center Panel

- Monaco Editor if feasible
- If Monaco is difficult, create a code editor placeholder and leave `TODO(Monaco)`
- Language selector: Python and JavaScript only
- File tab, even if MVP only supports one file

### Bottom or Right Panel

- Output console
- Run result
- Test result summary
- Runtime error display

### AI Assistant Panel

- Mock chat messages
- Buttons or options for:
  - Hint
  - Explain
  - Debug
  - Code review
- No real AI calls

---

## 7. Button Behavior for UI-First MVP

### Login Button

- Use mock login.
- Redirect based on selected role:
  - Student â†’ `/student/dashboard`
  - Admin â†’ `/admin/dashboard`

### Start Assessment Button

- Navigate to the workspace page.
- No real assessment attempt creation yet. Backend will later derive the active attempt from the authenticated user and assessment_id.

### Run Button

- Show fake loading for a short time.
- Then display mock stdout, stderr, and public test results.
- Do not execute code.

### Submit Button

- Show confirmation modal.
- Then display mock score/result.
- Do not run hidden tests.

### Ask AI / Hint Buttons

- Add mock AI messages to the chat panel.
- Do not call external AI APIs.

---

## 8. API Placeholder Rule

Where a real API will be needed later, add a TODO comment like this:

```ts
// TODO(API): POST /api/v1/executions/run
// Purpose: Send current code, language, assessment_id, and question_id. Backend derives user and active attempt from auth context.
// Current MVP behavior: use mock response only.
```

Important API placeholders include:

```text
POST /api/v1/auth/login
GET  /api/v1/auth/me
POST /api/v1/auth/logout

GET  /api/v1/student/dashboard
GET  /api/v1/student/assessments
GET  /api/v1/student/results

POST /api/v1/student/assessments/{assessment_id}/start
GET  /api/v1/student/assessments/{assessment_id}/context
GET  /api/v1/student/assessments/{assessment_id}/attempt
GET  /api/v1/student/assessments/{assessment_id}/workspace
PUT  /api/v1/student/assessments/{assessment_id}/workspace

POST /api/v1/executions/run
POST /api/v1/submissions/finalize

POST /api/v1/ai/chat

GET  /api/v1/admin/dashboard
GET  /api/v1/admin/assessments
GET  /api/v1/admin/reports
GET  /api/v1/reports/aggregate/{assessment_id}
```

---

## 9. Recommended File Structure

Use this structure for the first implementation unless an existing app already has a strong structure.

For the chosen Next.js App Router structure:

```text
src/
  app/
    login/
      page.tsx
    student/
      layout.tsx
      dashboard/page.tsx
      assessments/page.tsx
      assessments/[assessmentId]/start/page.tsx
      assessments/[assessmentId]/workspace/page.tsx
      results/page.tsx
    admin/
      layout.tsx
      dashboard/page.tsx
      assessments/page.tsx
      assessments/new/page.tsx
      assessments/[assessmentId]/page.tsx
      reports/page.tsx
      reports/[assessmentId]/page.tsx

  components/
    layout/
    auth/
    student/
    workspace/
    admin/
    ui/

  mocks/
    auth.mock.ts
    student-dashboard.mock.ts
    admin-dashboard.mock.ts
    assessments.mock.ts
    workspace.mock.ts
    reports.mock.ts
    ai.mock.ts

  lib/
    mock-api/
    types/
```

Structure rules:

- Keep mock data in `src/mocks`.
- Keep mock API-like helper functions in `src/lib/mock-api` and add `TODO(API)` comments there.
- Keep reusable TypeScript types in `src/lib/types`.
- Keep workspace-specific UI under `src/components/workspace`.
- Do not create backend API routes for this MVP.
- Do not add a database, ORM, sandbox runner, or external AI provider integration.

---

## 10. Visual Style

Use `ui-style-reference.md` for visual inspiration.

Preferred style:

- Dark premium dashboard
- Navy/purple gradients
- Cyan/purple accent colors
- Glassmorphism cards
- Rounded panels
- Professional AI coding platform feeling
- Clear visual separation between student and admin areas

Do not copy unrelated marketing content, fake testimonials, fake partners, or unrelated branding from the style reference.

---

## 11. Implementation Phases

### Phase 1 â€” App Shell and Routing

Build:

- Login page
- Student layout
- Admin layout
- Sidebar/top navigation
- Mock role-based navigation

### Phase 2 â€” Student Dashboard

Build:

- Dashboard summary cards
- Available assessment cards
- Recent activity
- Results preview

### Phase 3 â€” Student Workspace

Build:

- Problem statement panel
- Monaco/code editor area
- Language selector
- Timer
- Autosave indicator
- Run/Submit buttons
- Output console
- AI assistant panel

### Phase 4 â€” Admin UI

Build:

- Admin dashboard
- Assessment list
- Assessment create/edit form
- Question form
- Test case table
- Report list
- Report detail page

### Phase 5 â€” Polish

Build:

- Empty states
- Loading states
- Mock error states
- Responsive layout
- Demo-ready navigation

---

## 12. Done Criteria

This task is done when:

- The frontend can be navigated visually without a backend.
- Student and admin flows are visible.
- The coding workspace looks like a real browser IDE.
- Python and JavaScript can be selected in the editor.
- Run shows mock output.
- Submit shows mock score/result.
- AI assistant shows mock responses.
- Admin pages show mock assessment/report data.
- All future API integration points are marked with `TODO(API)`.
- No real code execution exists, and no real frontend-managed session_id is required.
- No backend/database/sandbox/AI-provider logic was added.

---

## 13. Suggested Prompt to Start the AI Agent

Use this prompt after the frontend stack is confirmed or scaffolded. For this project, prefer Next.js App Router unless an existing frontend app clearly uses another stack:

```text
Read these files first:

1. requirements.md
2. complete_frontend_api_list_and_backend_alignment.md
3. ui-style-reference.md
4. module2_frontend_ui_task.md

I am responsible for Module 2 only: the frontend UI and browser-based IDE workspace.

Implement only the frontend visual UI using mock data.
Do not implement backend logic, database logic, real code execution, sandbox, or real AI API calls.
Where backend APIs will be needed later, leave TODO(API) comments.

Before coding:
1. Inspect the existing project structure.
2. Report the planned files to create or modify.
3. Confirm that the implementation is frontend-only and mock-data-based.
4. Then implement the UI.

After coding:
1. Run typecheck/lint/build if available.
2. Report changed files.
3. Report which pages are implemented.
4. Report where TODO(API) comments were added.
5. Confirm that no real code execution, backend logic, database logic, or external AI API calls were implemented.
```




