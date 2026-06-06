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
  submission, result, report, and database state.
- Module 2 owns UI, frontend routes, API client behavior, workspace display, and
  preview/verification surfaces.
- Module 3 owns untrusted code execution, resource limits, test execution, and
  safe result schemas.
- Module 4 owns AI provider access, system prompts, telemetry, token tracking,
  and AI response safety.

## API Rules

- Standard responses use `{ "ok": true, "data": ... }` or
  `{ "ok": false, "error": { "code": "...", "message": "..." } }`.
- Backend-connected assessment flows are assessment-scoped.
- The frontend must not create, store, trust, or send real attempt/session
  identifiers.
- The backend resolves active attempts from authenticated user context and
  `assessment_id`.

## Security Requirements

- Hidden tests and grading implementation are not returned to student-facing
  APIs.
- Frontend does not call the database, sandbox, or external AI providers.
- Backend AI assistance uses configured provider-backed completion or returns a
  structured provider-unavailable error.
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
- Local doctor mode reports prerequisite and configuration readiness without
  starting services, restoring dependencies, or writing secrets.
- Root npm dependency restoration is lockfile-hash gated so repeated local
  starts do not reinstall duplicate dependency trees for the same
  `package-lock.json`.
- Local SQLite provisioning uses a stable gitignored file path so repeated
  starts do not create duplicate local database dependencies.
- Backend startup requires an explicit database connection string and configured
  seed administrator credentials in every environment.
- Student code is never executed in frontend JavaScript or normal backend
  request handlers.
- Secrets remain out of tracked files.

## Verification Requirements

- Frontend changes run typecheck and build at minimum.
- Frontend dependency or tooling changes also run lint and root `npm audit`.
- Backend changes run build and relevant tests.
- Cross-module changes run frontend checks, backend checks, and a reviewer pass.
