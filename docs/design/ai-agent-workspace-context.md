# AI Agent Workspace Context

## Problem Definition

The embedded AI agent must understand the active workspace, but the current
assistant request only sends selected language and active file content. It does
not send the active file name, visible sibling file contents, or the latest
public run feedback. The backend prompt asks for small snippets, while the
frontend applies the first Markdown code block as a full file replacement. This
can corrupt the current file and make the real sandbox fail even when the
student expected a targeted edit.

## Option Comparison

- Keep Markdown-only responses and extract the first code block: simple, but it
  cannot distinguish examples from file replacements.
- Add frontend-only heuristics for code block extraction: reversible, but still
  guesses intent from unstructured text.
- Add backend-shaped structured AI responses with optional apply metadata:
  slightly larger contract, but gives the UI a reliable boundary for when an
  edit is truly applicable.

Chosen option: backend-shaped structured responses. The response may include a
single active-file replacement suggestion. The frontend shows an Apply action
only when that structured suggestion exists and targets the current file.

## State Machine

States:

- `idle`: no AI request in progress.
- `context-collected`: frontend captured active file name, selected language,
  visible files, active file content, and latest public run result if present.
- `provider-pending`: backend is waiting for the configured real provider.
- `response-readable`: backend received a real provider response and produced
  student-visible Markdown.
- `edit-available`: the provider returned a validated active-file replacement.
- `edit-unavailable`: the provider returned explanation/debug guidance only or
  an invalid/untrusted edit target.
- `provider-error`: no usable real provider response was available.
- `edit-applied`: student explicitly applied the validated replacement to the
  current workspace file.

Events:

- `send-ai-request`
- `provider-success`
- `provider-unavailable`
- `structured-edit-valid`
- `structured-edit-invalid`
- `apply-edit`
- `run-public-checks`

Guards:

- The frontend does not send `session_id` or `attempt_id`.
- The backend resolves the active attempt from authenticated user and
  assessment ID.
- Only visible workspace data and public run output may enter the AI context.
- Hidden tests, hidden expected outputs, administrator notes, provider secrets,
  and server-side system prompts never enter frontend-visible data.
- Apply is shown only for structured suggestions targeting the active file and
  selected language.
- The selected language must be one of the active question's allowed student
  languages after workspace state normalization.

Transitions:

- `idle` + `send-ai-request` -> `context-collected`
- `context-collected` -> `provider-pending`
- `provider-pending` + `provider-success` -> `response-readable`
- `response-readable` + `structured-edit-valid` -> `edit-available`
- `response-readable` + `structured-edit-invalid` -> `edit-unavailable`
- `provider-pending` + `provider-unavailable` -> `provider-error`
- `edit-available` + `apply-edit` -> `edit-applied`
- `edit-applied` + `run-public-checks` returns to normal workspace execution
  through the real sandbox.

## Impact Surface

- Module 2 AI workspace request construction and Apply behavior.
- Module 4 AI request DTOs, prompt construction, structured response parsing,
  provider error handling, and interaction logging.
- Module 3 verification only through existing run API and sandbox behavior.
- Documentation and acceptance criteria for context-aware AI assistance.

## Rollback Path

- Remove optional AI request context fields and structured suggestion response.
- Restore Markdown-only assistant responses and hide Apply actions for code
  blocks until a replacement mechanism is reintroduced.

## Primitive Acceptance Criteria

- AI assist requests include the active file name, selected language, active
  file content, visible selected-language files, and latest public run feedback
  when available.
- AI assist requests do not send stale selected-language or visible-file
  language values that are disallowed by the active question.
- Backend prompts instruct the provider to return JSON with Markdown and an
  optional active-file replacement suggestion.
- The frontend shows Apply only for backend-validated structured suggestions,
  not for arbitrary Markdown code blocks.
- Applying a suggestion updates the intended active file only and does not send
  or trust a frontend-managed `session_id` or `attempt_id`.
- Running Task 1 after a validated AI-style active-file replacement uses the
  real sandbox and does not fail due to missing file context.
