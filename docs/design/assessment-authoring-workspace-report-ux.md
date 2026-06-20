# Assessment authoring, workspace navigation, and AI report summary UX

## Problem definition

The assessment creation flow combines question generation and delivery settings in one crowded step. The student workspace lists every task above the active problem statement, reducing reading space. Reports expose detailed AI evidence and reflection text but lack a concise synthesis that explains how the student used AI and whether the reflection demonstrates understanding.

## Option comparison

- Keep all controls visible: lowest implementation cost, but preserves crowding and weak information hierarchy.
- Add collapsible sections: reduces some visual weight, but still makes users manage several open sections.
- Use staged authoring, single-question navigation, and a synthesized report card: gives each decision one clear surface while reusing existing APIs and report data.

The third option is selected.

## State machines

### Assessment creation

- `basics` → title and description.
- `questions` → choose the task mix, generate a draft assessment, and review/edit generated tasks.
- `delivery` → duration, start, expiry, status, and AI availability.
- `complete` → save delivery settings and open the assessment detail page.

Generation creates a backend draft because generated tasks must be persisted before administrator review. If generation fails, the flow remains on `questions`. If final delivery save fails, the draft remains reviewable and the administrator stays on `delivery`.

### Workspace question navigation

- One question is active at a time.
- Previous/next navigation persists current editor state before switching.
- The task list is represented by compact progress indicators rather than full rows.
- First/last question disables the corresponding navigation control.
- Before entering the workspace, the expiration timestamp is explicitly labeled
  as the submission deadline and explains the transition to review-only access.
- The default IDE layout prioritizes editor height and width: output starts
  compact, the AI rail starts narrower, and its visible divider can be dragged.

### AI assessment summary

- Completed grading shows the stored automatic grading summary, a reflection-understanding statement, and concise usage evidence.
- Pending/failed grading shows an explicit status message without fabricating a summary.
- AI-disabled attempts do not show an AI assessment summary.

## Impact surface

- Administrator assessment creation page and existing assessment/question APIs.
- Student workspace task sidebar only; editor, sandbox, submission, and AI boundaries remain unchanged.
- Student review and administrator report presentation using existing API fields.

## Rollback path

The staged frontend can be reverted to the existing two-step layout without data migration. Workspace navigation can return to the task list because active-question state is unchanged. Report summary cards can be removed without changing stored grading data.

## Primitive acceptance criteria

- Administrators complete basics, question generation/review, and delivery settings as three distinct steps.
- Generated questions are reviewable before delivery settings are finalized.
- Students see one active problem statement with previous/next question controls and a clear position indicator.
- Student and administrator reports show a concise AI-use and reflection-understanding synthesis when grading is complete.
- Pending or failed grading never appears as a completed narrative.
