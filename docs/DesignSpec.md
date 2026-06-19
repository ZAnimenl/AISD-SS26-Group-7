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

## Submission Reflection Design

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
- AI-disabled result views show only the Functional Score.
- The Final Score card explains that it is the arithmetic mean of the other two
  scores.
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
