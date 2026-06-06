# Product Requirements Document

## Product Summary

The product is an AI-assisted online coding assessment platform with student and
administrator roles, a browser IDE, sandboxed execution, embedded AI assistance,
and reporting.

## Core Functional Requirements

- Students and administrators authenticate before protected access.
- Administrators create, edit, archive, and review assessments.
- Assessments contain practical tasks with task type, difficulty, supported
  languages, starter files, verification mode, and grading configuration.
- Students open an assessment, edit files, run public checks, use embedded AI
  assistance when enabled, and submit final work.
- Submissions are graded through sandboxed execution.
- Reports show score outcomes, AI interactions, total tokens, and token
  efficiency indicators.

## First Implementation Task Categories

- Frontend UI extension.
- REST API development.
- Database query/schema work.
- Bug fixing in existing code.

## Key Product Rules

- Frontend UI extension tasks require direct browser UI preview.
- Other task types use appropriate verification output such as API responses,
  database result tables, automated tests, or regression tests.
- The frontend must not send a real `session_id` or `attempt_id`.
- Student-facing surfaces must not expose hidden tests, hidden expected outputs,
  grading implementation, or administrator-only notes.
- AI-generated drafts must be administrator-reviewed before publication.

## Acceptance Link

Final acceptance criteria are maintained in `ACCEPTANCE.md`.
