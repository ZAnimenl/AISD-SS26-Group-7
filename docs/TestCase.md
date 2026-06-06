# Test Case Catalogue

This catalogue defines behavior-level tests to keep implementation aligned with
the active requirements. It does not replace automated test files.

## Authentication and Role Access

- Unauthenticated users are blocked from protected student and administrator
  routes.
- Students cannot access administrator-only features.
- Administrators can access assessment management and reports.

## Assessment Management

- Administrators can create an assessment with title, description, duration,
  status, AI enabled state, and shared prototype reference.
- Administrators can create tasks using the supported task categories.
- AI-generated assessment drafts are editable before publication.

## Student Workspace

- Students can start an active assessment and open the browser workspace.
- Workspace context displays public task details and starter files.
- Workspace APIs do not require frontend-sent `session_id` or `attempt_id`.
- Autosave persists selected language, active file, file contents, and version.

## Run and Submit

- Run uses public checks and returns safe stdout, stderr, status, and public test
  feedback.
- Submit evaluates final work and returns visible and hidden test summary counts
  without hidden inputs or expected outputs.
- Expired or closed attempts reject new runs and submissions.

## AI Assistance

- AI assistance is hidden or blocked when disabled for the assessment.
- AI interactions record message, response, semantic tags, input tokens, output
  tokens, total tokens, assessment, task, and attempt ownership.
- Direct complete-solution requests receive a safety response rather than a full
  answer.

## Reporting

- Administrator reports include score, status, AI interaction count, total
  tokens, average tokens per interaction, and token efficiency indicator.
- Student-facing result views do not expose hidden tests or administrator notes.

## Security

- Frontend code does not access database, sandbox, or external AI provider
  APIs directly.
- Student code is not executed by frontend JavaScript or normal backend request
  handlers.
- Secrets and provider keys are not committed.
