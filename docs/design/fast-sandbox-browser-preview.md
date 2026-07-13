# Fast sandbox browser preview

## Problem

Browser UI runs currently execute the generated preview check and every public check as separate Jest/jsdom containers. A warm JavaScript check takes several seconds; running three of them concurrently can push all three beyond the ten-second host deadline. The run endpoint waits for the complete batch, so the student receives neither a preview nor a useful preview failure until the whole batch times out.

## Decision

Keep preview generation inside the existing network-disabled grader container, but give the platform-generated browser-preview test a lightweight packaging command instead of Jest/jsdom. The trusted packaging script reads the selected local HTML entry, inlines local CSS and JavaScript as text, rejects unsafe entry paths, removes remote assets, validates that the result contains HTML, and writes `actual.txt`. It does not execute student JavaScript.

Public checks continue to execute concurrently in their existing isolated containers. Their command deadline is shortened so a broken or hanging generated check returns an explicit timeout and the complete run response stays inside the ten-second warm-run budget. The sandbox host deadline remains a final guard above the command deadline.

The frontend renders the sandbox-produced document as soon as the run response arrives. If the completed run contains no preview document, it shows a timeout/runtime/failure state instead of the indefinite “not available yet” placeholder.

## State flow

1. Run requested.
2. Synthetic preview packaging and public checks start concurrently.
3. Preview packaging reaches one of `ready`, `invalid`, `timed_out`, or `sandbox_unavailable`.
4. Public checks reach `passed`, `failed`, `runtime_error`, or `timed_out` within their bounded deadline.
5. The endpoint returns the complete result. A successful preview remains available even when another public check fails or times out.

## Security and boundaries

- Preview packaging uses the same container isolation, disabled network, dropped capabilities, memory/CPU limits, and temporary workspace as grading.
- The packager handles only safe basename files from the submitted workspace and never evaluates student code.
- The browser iframe keeps its restrictive CSP and sandbox attributes.
- Hidden tests are unchanged and no hidden test source or output is added to the preview response.
- Only synthetic tests marked `source=browser_ui_preview_run` can select the fast path.

## Timing budget

- Preview packaging command: 3 seconds maximum.
- Ordinary grader command: 9 seconds maximum.
- Host/container guard: 9.5 seconds maximum, including queueing and startup after image readiness.
- Warm API acceptance: return a preview or an explicit failure in less than 10 seconds.
- Cold image preparation keeps the existing quick retryable “sandbox warming up” response.

## Failure handling and rollback

Missing or invalid HTML produces a normal failed preview check. Sandbox startup failure produces the existing retryable internal error. Hanging public code is terminated and reported as `time_limit_exceeded`; it cannot suppress a successfully packaged preview.

Rollback is local: remove the synthetic-preview fast-path branch and restore the previous grader deadlines. No schema or persisted-data migration is involved.

## Verification

- Unit coverage identifies only the platform synthetic preview and writes a standalone packager workspace.
- Docker integration coverage verifies HTML/CSS/JS inlining and the sub-ten-second warm path.
- Frontend coverage verifies running, timeout, runtime-error, and initial placeholder copy.
- Full backend and frontend regression suites remain required before handoff.
