# Mid-Term Status

This document summarizes the current as-is project state for the mid-term presentation.

## Motivation / Project Overview

The project is an AI-assisted online coding assessment platform. It addresses the shift from traditional coding tests, which mostly evaluate final code, toward AI-assisted development workflows where instructors also need to understand process, AI usage, and assessment integrity.

The platform lets administrators create assessments and questions, lets students solve tasks in a browser IDE, runs submissions through backend-controlled grading, records AI assistance interactions, and shows results/reports to administrators. The updated product direction expands this into structured AI hints, AI credit budgets, AI Rescue, task generation, reflection, process-aware scoring, and controlled student report release.

## Current Architecture

The project follows four modules:

1. **Identity and Assessment Management**
   Authentication, roles, users, assessments, questions, attempts, workspace persistence, submissions, results, reports, and PostgreSQL persistence.

2. **Interactive Browser-Based Workspace / Frontend IDE**
   Next.js student/admin UI, Monaco editor, dashboards, assessment pages, workspace autosave, run/submit controls, structured AI assistance panel, and frontend API client.

3. **Sandboxed Code Execution and Evaluation**
   Backend-controlled Docker grading for Python, JavaScript, and TypeScript, including test execution, stdout/stderr capture, timeouts, and hidden-test protection.

4. **AI Telemetry and Assistance**
   Backend AI chat endpoint, AI interaction logging, semantic tags, optional OpenAI-compatible provider configuration such as DeepSeek, and fallback mock guidance for local demos.

## Current Pipeline

1. User logs in with backend bearer-token authentication.
2. Student starts or resumes an assessment attempt.
3. Backend resolves the active attempt from authenticated user + assessment.
4. Student edits code in the browser workspace.
5. Workspace state is restored/autosaved through backend APIs.
6. Run executes public/sample tests through the backend grading flow.
7. Submit evaluates final code and stores submission results.
8. AI assistance requests are sent to the backend, logged, and returned to the student.
9. Admin pages show dashboards, assessment state, reports, submissions, and AI usage summaries.

## Verification Evidence

Current checks used for handoff confidence:

```powershell
dotnet test Backend\Backend.sln -v:minimal
npm run build
```

Latest observed results:

- Backend tests: 32 passed.
- Frontend production build: compiled successfully and generated all app routes.

Relevant verification coverage includes:

- API response contract tests.
- Backend configuration tests.
- Hidden-test projection tests.
- Submission/result tests.
- Docker grading tests and Docker availability-aware integration tests.
- Frontend type/build verification through Next.js.

## Development Process With AI

The team uses a spec-driven agent workflow:

- `AGENTS.md` is the orchestration guide.
- `.agents/skills/` contains specialized agent roles for routing, module-specific implementation, integration, review, and handoff.
- `prompt-commander` routes tasks and chooses the skill chain.
- Module skills keep work within the four-module architecture.
- `strict-code-reviewer` checks latest changes for spec, API, auth, hidden-test, sandbox, AI, and build/test risks.
- `mcp-code-analyzer` provides heuristic requirements-compliance scans against `SPEC.md`.

This process is intended to make AI assistance auditable instead of ad hoc.

## Current Demo Scope

Implemented or connected:

- Login/register/logout/current-user APIs.
- Student and administrator roles.
- Student dashboard, assessments, workspace, results, and review pages.
- Admin dashboard, assessments, question/test-case editing, reports, and users page.
- Backend-backed assessment attempts and workspace persistence.
- Docker-based run/submit grading.
- AI chat endpoint with logging, optional DeepSeek/OpenAI-compatible provider configuration, and fallback guidance.
- PostgreSQL-backed data model and seeded demo users.
- One-command local startup through `npm run dev`.

Important safety boundaries:

- Frontend never receives hidden test inputs or expected outputs.
- Frontend never calls the sandbox directly.
- Frontend never calls external AI providers directly.
- Frontend does not create, store, trust, or send a real `session_id`.
- Backend owns authenticated attempt resolution.

## Known Limitations / Next Steps

- Harden Docker sandboxing for production beyond local-demo limits.
- Add browser/E2E tests for the main demo path.
- Improve frontend route guards and unauthorized-state UX.
- Expand DeepSeek/provider configuration, rate handling, and provider error handling.
- Add richer report drilldowns and raw AI interaction review where appropriate.
- Decide whether multi-file workspaces are required for the final version.
- Improve deployment documentation and final demo setup reliability.


