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
- Submissions receive a `0-100` Functional Score through sandboxed execution.
- AI-enabled submissions require platform AI use and a maximum-100-word
  reflection completed within ten minutes after code is frozen.
- AI-enabled submissions receive a separate automatic `0-100` AI Usage Score
  and a Final Score equal to the arithmetic mean of the Functional and AI Usage
  scores.
- AI-disabled submissions receive only the Functional Score and do not require
  a reflection.
- Reports show criterion scores, grading evidence, interaction events,
  reflection, and descriptive token metrics.
- Generated tasks record an administrator-only, provider-measured compact
  reference baseline; the student AI panel shows descriptive per-task density
  and context-coverage metrics without exposing grading internals. For a fully
  passing task, those metrics provide a bounded reference-relative contribution
  to AI Usage scoring; unavailable provider measurements do not penalize a
  student.

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
- AI Usage Score weights are Prompt quality and context 30%, Token and
  interaction efficiency 40%, Critical evaluation and adaptation 20%, and
  Reflection quality and consistency 10%.
- Token and interaction efficiency uses a semantic behavioral assessment, a
  0-15 deterministic reference-efficiency component for measured fully passing
  tasks, and 10 points of objective repetition metrics. If no measured task is
  used, the semantic behavioral assessment retains its legacy 30-point range.
- AI grading does not use a fixed absolute token threshold or cohort-relative
  token usage.
- Automatic AI grading failures preserve the Functional Score and surface a
  pending or failed grading state rather than assigning zero.

## Acceptance Link

Final acceptance criteria are maintained in `ACCEPTANCE.md`.
