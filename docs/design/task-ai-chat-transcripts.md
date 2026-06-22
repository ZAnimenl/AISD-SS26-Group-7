# Task AI Chat Transcripts

## Problem

Students need an independent embedded AI conversation for every assessment task and a durable record of the prompts and responses exchanged in that conversation.

## Decision

Persist interactions in the existing assessment-scoped `ai_interactions` store, expose a student-authorized transcript endpoint filtered by assessment and task, and restore that transcript whenever its task is opened. The workspace can export the returned transcript as a JSON file for the active task.

## State machine

- `unloaded` -> `loading` when a task's AI panel is opened.
- `loading` -> `ready` when its persisted transcript is returned.
- `ready` -> `waiting_for_response` when the student sends a prompt.
- `waiting_for_response` -> `ready` when the AI response is persisted and shown.
- Any state -> `unavailable` when the transcript request fails; the existing visible messages remain usable.
- `ready` -> `exported` when the student downloads that task's JSON transcript; the persisted conversation remains unchanged.

## Security and rollback

The endpoint authorizes the current student against their own assessment session and returns only that task's stored prompt, response, type, timestamp, and token counts. It returns no hidden tests, server prompts, provider details, or other students' interactions. Removing the endpoint and transcript UI returns the workspace to its current single-session chat display; existing interaction records remain intact.

## Acceptance

- Switching tasks shows that task's own AI history rather than another task's messages.
- Refreshing the workspace restores previously persisted prompts and responses for each task.
- The active task transcript can be downloaded as JSON containing every persisted prompt and response for that task.
