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

## Assessment Authoring Design

- Assessment creation has three explicit stages: Assessment basics, Generate
  and review, and Delivery settings.
- Generation persists a draft before review so the existing question and test
  editor remains the single authoring surface.
- Timing, start mode, expiration, publication status, and AI availability are
  finalized only after at least one question has been reviewed.

## Workspace Design

- The workspace shows task description, file navigation, code editor, task type,
  supported language, run output, submit state, and AI assistance when enabled.
- The code editor receives the largest share of the initial workspace. The
  output panel starts compact, while the narrower AI panel remains horizontally
  resizable through a visible drag handle.
- Editor file tabs use a dedicated horizontally scrollable row. Language and
  Run controls use a separate row so controls cannot overlap at narrow panel
  widths. The AI header uses a compact usage row beneath its title.
- The task rail shows one active question with Previous, Next, and compact direct
  navigation instead of rendering every question as a full row.
- Question changes persist the current editor state and restore the selected
  question's language, file, code, and run context.
- Each task has an independent embedded AI conversation. Opening a task restores
  only its persisted prompts and responses, and students can download that
  task's JSON transcript.
- The AI panel shows token and interaction totals for the active task only;
  assessment-wide totals remain available to reporting surfaces.
- The verification area adapts to task type.
- Frontend UI extension tasks show direct browser UI preview.
- Browser preview renders only sandbox-produced HTML; when no preview output is
  returned, it shows a no-output state instead of sample task content.
- REST API, database, and bug-fix tasks show task-appropriate verification
  output rather than forcing a browser preview.

## Submission Reflection Design

- AI-enabled student and administrator reports place a concise AI-use and
  demonstrated-understanding summary below the score overview.
- On the student review page, the automated assessment insight appears directly
  above the student's submitted reflection within the submission summary.
- The summary uses stored grading evidence, reflection consistency, and, where
  available, interaction, token, and confidence data. The detailed rubric and
  full reflection remain separate supporting evidence.

- AI-enabled final submission first freezes the code and then opens a dedicated
  reflection step.
- The reflection prompt is: "In no more than 100 words, explain how you used
  the AI assistant during this assessment. Include one suggestion that helped
  and how you verified it, and one suggestion that you rejected, corrected, or
  found unhelpful."
- The reflection view shows a live word count, a visible ten-minute countdown,
  autosave status, and an early-submit action.
- Code editing, Run, AI assistance, and code resubmission are unavailable after
  code is frozen.
- Refreshing or reconnecting restores the latest draft and backend-owned
  deadline without restarting the timer.
- At timeout, the latest saved reflection is submitted automatically.
- AI-disabled submission bypasses the reflection step.

## Results and Report Design

- AI-enabled result views show three distinct score cards: Functional Score,
  AI Usage Score, and Final Score.
- Percentage score cards place the percentage inside a compact donut chart
  without repeating a larger percentage outside it. These charts visualize the
  score itself and do not imply a historical trend.
- AI-disabled result views show only the Functional Score.
- The student Final Score card places its label below the score donut and keeps
  the presentation concise.
- Administrator report detail exposes the four AI Usage Score criteria,
  automatic-grading summary, cited evidence, reflection consistency, raw
  interaction and token metrics, and grading status.
- Token totals are labeled as descriptive evidence and are not presented as a
  universal efficiency threshold.
- Pending and failed AI grading states remain visibly distinct from a zero AI
  Usage Score.

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
# Confirmed 2026-06-19 implementation additions

## Canonical assessment prototype

`assessmentPrototype/` is the source-only canonical Todo application used for assessment task contracts. It contains browser-safe HTML/CSS/JavaScript plus FastAPI, Peewee, and SQLite source. It is not imported into the Next.js frontend or normal ASP.NET backend and runs only through sandbox assessment execution.

Generated tasks must be focused extensions of the canonical Todo entity (`id`, `title`, `description`, `completed`), REST routes, and module layout. The base application may not be regenerated as another framework or product.

## Preview boundary

For browser-preview tasks, the sandbox public preview check reads the submitted HTML and inlines local CSS and JavaScript into a bounded `preview_document`. Module 2 renders that document in a unique-origin iframe with scripts/forms enabled and CSP blocking network, parent access, navigation, plugins, and nested frames. Non-UI tasks retain task-specific verification views and the console separates assertion failures, runtime errors, and sandbox infrastructure failures.

## Submission freeze

The workspace enters a frozen state immediately when confirmed submission starts. The displayed timer is frozen, all code/task/file/language/Run/AI/suggestion controls are disabled, and duplicate submissions are blocked. Backend rejection returns the workspace to editable state. Backend confirmation routes AI-enabled attempts to reflection and AI-disabled attempts to review.

## Shared dropdown

All prior native selects use `CustomDropdown`, which owns keyboard navigation, focus movement, outside-click closing, disabled behavior, and hidden-input form serialization.

## Assessment expiration

Administrators configure a required assessment deadline separately from the start time and per-attempt duration. The backend validates and persists the deadline, caps each new session at the earlier of its normal duration or the assessment deadline, and rejects student mutation/execution flows after the deadline. Expired assessments remain visible so submitted results can be reviewed.
