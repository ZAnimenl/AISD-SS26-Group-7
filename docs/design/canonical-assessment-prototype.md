# Canonical Assessment Prototype and Sandbox Preview

## Problem

Assessment tasks need one real Todo application contract instead of unrelated generated base applications. Students must edit platform-managed files without installing dependencies, and browser previews must execute only sandbox-produced output.

## Options

- Keep generating standalone starter applications: rejected because contracts drift and the base application becomes fictional.
- Embed the React/Vite source directly: rejected because students would need a bundler and dependency installation.
- Preserve the Todo API contract and adapt its visible UI to browser-safe HTML/CSS/JavaScript: selected because it is portable, reviewable, and runnable in the sandbox.

## State machine

### Preview

- `idle` -> `running` when Run is requested.
- `running` -> `verified` when public checks finish and a sandbox preview document is returned.
- `running` -> `failed_tests` when checks execute but assertions fail.
- `running` -> `runtime_error` when student code fails during execution.
- `running` -> `infrastructure_failure` when the sandbox cannot execute.
- Any completed state -> `running` on a later Run.

### Submission

- `editable` -> `frozen_pending_confirmation` when the final backend submission starts.
- `frozen_pending_confirmation` -> `reflection_pending` after an AI-enabled submission is confirmed.
- `frozen_pending_confirmation` -> `completed` after an AI-disabled submission is confirmed.
- `frozen_pending_confirmation` -> `editable` only when the backend rejects or fails the submission.
- `reflection_pending` -> `completed` after reflection submission or timeout.

## Impact surface

- Module 1 owns attempt/submission state and report aggregation.
- Module 2 owns frozen controls, preview rendering, dropdown interaction, and report presentation.
- Module 3 owns preview-document production, execution classification, and isolation.
- Module 4 owns AI evidence normalization and scoring.
- `assessmentPrototype/` is a sandbox-only source asset and is not imported by Next.js or ASP.NET runtime code.

## Security boundary

The iframe receives only a document returned by the sandbox. It uses a unique origin, allows scripts and forms, blocks network access through CSP, and has no parent/top-navigation permissions. Student code is never evaluated by Next.js or the normal ASP.NET request process.

## Rollback

Remove the `preview_document` response field and restore the non-interactive preview renderer. The canonical prototype directory is isolated and can be removed without changing platform runtime dependencies.

## Primitive acceptance criteria

- The canonical source contains browser-safe HTML/CSS/JavaScript, FastAPI, Peewee, and SQLite contracts.
- Local CSS and JavaScript are inlined into the preview artifact inside the sandbox.
- Browser previews support forms, JavaScript, SVG, canvas, dependency diagrams, and locally bundled charts without network access.
- Non-UI tasks show task-specific public verification.
- Submission immediately freezes all workspace mutations until backend failure or routing.
- Students do not install prototype dependencies locally.

