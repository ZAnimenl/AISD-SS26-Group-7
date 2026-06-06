# Design Specification

## Product Experience

The application is a practical assessment workspace, not a marketing site. UI
surfaces should prioritize clarity, scanning, task progress, code editing, run
feedback, and report review.

## Information Architecture

- Login routes authenticate users.
- Student routes expose dashboard, available assessments, assessment start,
  workspace, review, and results.
- Administrator routes expose dashboard, assessment management, user management,
  reports, and assessment detail workflows.

## Workspace Design

- The workspace shows task description, file navigation, code editor, task type,
  supported language, run output, submit state, and AI assistance when enabled.
- The verification area adapts to task type.
- Frontend UI extension tasks show direct browser UI preview.
- Browser preview renders only sandbox-produced HTML; when no preview output is
  returned, it shows a no-output state instead of sample task content.
- REST API, database, and bug-fix tasks show task-appropriate verification
  output rather than forcing a browser preview.

## Visual Constraints

- Keep UI quiet, dense, and work-focused.
- Do not let visual style override product requirements, route behavior,
  authentication, API contracts, or hidden-test protection.
- Avoid introducing decorative styling that reduces readability or makes the
  workspace harder to scan.

## Accessibility and Robustness

- Important actions must have visible labels or accessible names.
- Loading, error, empty, expired, and submitted states must be visible.
- Text must fit in its container across supported viewport sizes.
