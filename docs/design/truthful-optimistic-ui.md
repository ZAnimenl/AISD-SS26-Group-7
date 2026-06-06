# Truthful Optimistic UI

## Problem Definition

Real deployments can add latency for database startup, provider-backed AI draft
generation, autosave, sandbox execution, and final submission. The UI must keep
users informed without pretending that the backend has already completed work.

## Option Comparison

- Treat requests as blocking and leave controls unchanged: rejected because users
  can perceive normal latency as a stuck application.
- Optimistically show final success before the backend confirms: rejected because
  it fabricates assessment, AI, execution, or submission state.
- Optimistically show only local intent and in-flight progress: selected. The UI
  can show that a request was sent, preserve typed input, disable duplicate
  actions, and expose retry/error states, but it must wait for real backend
  confirmation before showing saved, generated, passed, or submitted states.

## State Machine

### Data Loading

- States: idle, loading, loaded, failed.
- Events: route mounted, backend response received, backend error received,
  retry requested.
- Guards: authenticated pages still redirect on authentication failures.
- Transitions: loading to loaded only after real response; loading to failed
  after real error.
- Side effects: render skeleton or empty-progress state; never show fake rows.
- Failure paths: show backend error and retry/navigation affordance.
- Rollback path: restore prior list rendering.

### Mutating Admin Actions

- States: idle, pending, confirmed, failed.
- Events: submit clicked, backend confirms, backend rejects.
- Guards: disable duplicate action while pending.
- Transitions: pending to confirmed only after backend response; pending to
  failed preserves edited local form values for correction.
- Side effects: show operation-specific pending copy.
- Failure paths: show backend error; do not remove or add entities until
  confirmed.
- Rollback path: restore prior action handlers.

### AI Assistance

- States: idle, local-message-pending, provider-pending, provider-completed,
  provider-failed.
- Events: student sends message, provider returns response, provider fails.
- Guards: AI must be enabled for assessment and request must go through backend.
- Transitions: local user message may appear immediately as sent intent; assistant
  answer appears only after provider-backed backend response.
- Side effects: append pending assistant placeholder; replace it with real
  provider content or a failure message.
- Failure paths: show provider/backend error without fabricated guidance.
- Rollback path: restore prior AI chat rendering.

### Run And Submit

- States: idle, saving, running, submitting, completed, failed.
- Events: run clicked, submit confirmed, autosave completed, sandbox response
  received, backend error received.
- Guards: prevent duplicate run/submit while the same action is pending.
- Transitions: run/submission success only after backend response; sandbox
  unavailable remains an explicit dependency error.
- Side effects: show pending status, disable duplicate controls, keep editor
  input intact.
- Failure paths: show backend error and keep user in workspace.
- Rollback path: restore prior workspace action handling.

## Impact Surface

- Module 2 UI components for admin creation/editing, list loading, workspace AI,
  run, and submission states.
- Frontend API client error display remains unchanged at the contract boundary.
- No backend endpoint shape, auth, database, sandbox, or AI provider contract is
  changed by this UI mechanism.

## Primitive Acceptance Criteria

- Long-running LLM draft generation visibly enters a provider-backed pending
  state and disables duplicate generation until the backend responds.
- Admin question/test mutations visibly show the pending action and do not claim
  success before backend confirmation.
- AI chat displays the user's sent intent immediately and displays no assistant
  answer until a real backend/provider response arrives.
- Final submission visibly shows saving/submitting progress and prevents duplicate
  submission clicks.
- Data-loading pages show loading or failed states instead of looking empty while
  waiting for the backend.
- Error states preserve user-entered data and expose the real backend error
  message.
