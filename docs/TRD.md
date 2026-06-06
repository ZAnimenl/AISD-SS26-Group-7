# Technical Requirements Document

## System Shape

- Frontend: Next.js App Router, TypeScript, Tailwind CSS, and Monaco editor.
- Backend: ASP.NET Core API.
- Persistence: PostgreSQL through EF Core.
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
- Student code is never executed in frontend JavaScript or normal backend
  request handlers.
- Secrets remain out of tracked files.

## Verification Requirements

- Frontend changes run typecheck and build at minimum.
- Backend changes run build and relevant tests.
- Cross-module changes run frontend checks, backend checks, and a reviewer pass.
