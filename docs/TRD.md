# Technical Requirements Document

## System Shape

- Frontend: Next.js App Router, TypeScript, Tailwind CSS, and Monaco editor.
- Backend: ASP.NET Core API.
- Persistence: SQLite through EF Core for local one-command startup, and
  PostgreSQL through EF Core for explicit external database deployment.
- Execution: sandboxed evaluation service owned by Module 3.
- AI: backend-owned AI assistance and telemetry service owned by Module 4.

## Module Boundaries

- Module 1 owns identity, assessment, attempt, workspace persistence,
  submission, reflection, score, result, report, and database state.
- Module 2 owns UI, frontend routes, API client behavior, workspace display, and
  preview/verification surfaces, suggestion event capture, and reflection UI.
- Module 3 owns untrusted code execution, resource limits, test execution, and
  safe result schemas.
- Module 4 owns AI provider access, system prompts, telemetry, token tracking,
  automatic AI usage grading, rubric versioning, and AI response safety.

## API Rules

- Standard responses use `{ "ok": true, "data": ... }` or
  `{ "ok": false, "error": { "code": "...", "message": "..." } }`.
- Backend-connected assessment flows are assessment-scoped.
- The frontend must not create, store, trust, or send real attempt/session
  identifiers.
- The backend resolves active attempts from authenticated user context and
  `assessment_id`.
- Pending registration challenges are short-lived backend state keyed by email.
  They do not reserve usernames; registration completion rechecks persisted
  email and username ownership before inserting the user. Pending operations
  are atomic per email, and completion claims are serialized within the process
  that owns those pending challenges.

## Security Requirements

- Hidden tests and grading implementation are not returned to student-facing
  APIs.
- Frontend does not call the database, sandbox, or external AI providers.
- Backend AI assistance uses configured provider-backed completion or returns a
  structured provider-unavailable error.
- Automatic AI grading uses a configured provider, fixed rubric version,
  validated structured output, and criterion-level evidence.
- AI grading provider, timeout, or schema failures preserve the functional
  submission and produce a retryable pending or failed grading state rather
  than a zero score.
- AI-generated assessment and question drafts use configured provider-backed JSON
  output or return a structured generation error.
- Local one-command startup restores project dependencies, creates a
  repository-owned SQLite database file when local database config is missing,
  prompts only for AI provider secrets, writes local secrets only to
  `.env.local`, and waits for backend health before starting the frontend.
- Local startup writes `Database__Provider=Sqlite` plus a SQLite connection
  string automatically and reports concrete repair steps for local SQLite
  regeneration, external database configuration, Docker sandbox permission, or
  system-runtime failures.
- Local startup and doctor mode detect common Docker Desktop and Colima socket
  locations and report sandbox runtime readiness without making Docker a
  startup requirement.
- Local startup normalizes accidental repeated DeepSeek key pastes and disables
  stale `LocalLlm__*` overrides so old local files do not activate a second AI
  provider path.
- Local startup resolves Windows npm shims to executable commands and prints
  the frontend URL before handing over to Next.js.
- Local startup restarts an old local Next.js listener on the frontend port when
  the process can be safely identified, so the printed frontend URL serves the
  current checkout instead of stale code.
- Local startup runs the backend `--seed-admin-only` path before backend health
  reuse/start so the configured seed administrator is repaired through the same
  EF Core path as normal backend startup.
- Local startup restarts an existing local Backend listener on the configured
  backend port when its process can be safely identified, then verifies reused
  external backend health with the configured seed administrator login.
- Local doctor mode reports prerequisite and configuration readiness without
  starting services, restoring dependencies, or writing secrets.
- Root npm dependency restoration is lockfile-hash gated so repeated local
  starts do not reinstall duplicate dependency trees for the same
  `package-lock.json`.
- Local SQLite provisioning uses a stable gitignored file path so repeated
  starts do not create duplicate local database dependencies.
- Local development login exposes the seeded administrator credentials through a
  development-only quick fill action; auth storage is cleared on backend 401 or
  logout, not on login page mount or non-auth data errors.
- Frontend bearer tokens and normalized users are stored in window-scoped
  `sessionStorage`, allowing separate administrator and student windows. Login,
  registration, Google callback, logout, and HTTP 401 handling affect only the
  current window. See `docs/design/window-scoped-authentication.md`.
- Backend startup requires an explicit database connection string and configured
  seed administrator credentials in every environment.
- Student code is never executed in frontend JavaScript or normal backend
  request handlers.
- Operational secrets should remain out of tracked files. The current private
  course checkout still contains dev-only Google OAuth and SMTP values in
  `Backend/Backend/appsettings.Development.json`; rotate/remove them before
  public release and prefer environment variables, `.env.local`, user-secrets,
  or hosting secret managers.

## AI-Enabled Submission Requirements

- The backend owns the transition from active attempt to frozen code,
  reflection pending, AI grading pending, and completed.
- AI-enabled final submission requires at least one successfully persisted AI
  interaction.
- The backend starts and enforces the ten-minute reflection deadline after
  final code is frozen.
- Reflection drafts are autosaved, limited to 100 words, and automatically
  finalized at the backend deadline.
- The frontend records response-visible and suggestion-decision events,
  including elapsed monotonic time, while the backend records authoritative
  receipt timestamps.
- A rapid unchanged acceptance within three seconds is stored as objective
  grading evidence, with the bounded deduction defined in
  `docs/design/automatic-ai-usage-scoring.md`.
- Deterministic repetition metrics are calculated by platform logic. The
  grading LLM may explain but may not override those fixed measurements.
- For generated tasks with a completed administrator-only reference baseline,
  platform logic also calculates a bounded 0-15 reference-efficiency component
  from prompt/response CpT and TpC, context coverage, and compact-reference-
  relative token cost. It is available only after a fully passing submission;
  unavailable baselines preserve the legacy semantic behavioral score.
- A completed reference baseline stores two to five minimal-input standard
  steps with public verification only. Hidden tests and grading implementation
  are never sent to the baseline provider or returned to students.
- Functional and AI usage grading remain separate. Module 4 cannot modify the
  Functional Score produced from Module 3 evaluation results.
- No absolute token cutoff or cohort-relative token measure contributes to the
  AI Usage Score.

## Verification Requirements

Sandbox execution follows `docs/design/sandbox-run-latency.md`: grader image
readiness is coalesced, student requests use a bounded readiness wait, and each
check runs in a resource-limited ephemeral container that mounts only its own
workspace and stages execution onto container-local storage.

- Frontend changes run typecheck and build at minimum.
- Frontend dependency or tooling changes also run lint and root `npm audit`.
- Backend changes run build and relevant tests.
- Cross-module changes run frontend checks, backend checks, and a reviewer pass.
