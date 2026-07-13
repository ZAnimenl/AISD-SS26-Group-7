# Bounded Parallel JavaScript Draft Generation

## Problem definition

Generated REST API and bug-fix tasks default to Python and JavaScript, but the
provider prompt describes the REST implementation as Python-only and the
canonical prototype has no JavaScript backend modules. The parser also validates
the provider-returned language list before applying the administrator-requested
languages. A provider response can therefore add `javascript` with only one file
and fail with a starter-file validation error even when the requested contract
was different.

Assessment blueprints generate each question and each administrator-only token
efficiency baseline sequentially. This preserves correctness but makes latency
grow linearly with the number of questions.

## Option comparison

### Keep provider-defined languages

Rejected. Provider output is untrusted draft content and must not override an
administrator-selected execution contract.

### Fabricate missing JavaScript files in the frontend

Rejected. This hides invalid backend state, produces generic files unrelated to
the generated task, and makes persisted authoring data depend on a UI fallback.

### Authoritative languages plus canonical JavaScript backend

Selected. Normalize required languages before the provider call, require the
response to use that exact set, merge immutable canonical modules, and then
validate starter files and per-language tests. JavaScript REST and bug-fix tasks
use platform-packaged Node/Express modules and the sandbox's baked Jest and
Supertest dependencies.

### Unbounded parallel generation

Rejected. It can create provider bursts, amplify retries and cost, and overload a
local provider.

### Bounded parallel generation

Selected with a global limit of two pipelines. DeepSeek documents account-level
concurrency and HTTP 429 behavior; a limit of two is a conservative local guard
and is also suitable for a local provider. Provider reference:
https://api-docs.deepseek.com/quick_start/rate_limit/

## State machine

### States

- `planned`: indexed task type, prompt, required languages, and score are fixed.
- `waiting_for_generation_slot`: task is queued behind the global limit.
- `generating`: one task owns a slot and may perform a provider call.
- `validating`: provider JSON, exact languages, test coverage, and starter
  structure are checked.
- `retrying`: validation failed and the task has attempts remaining.
- `generated`: a complete in-memory question graph exists.
- `waiting_for_baseline_slot`: generated task waits for bounded baseline work.
- `baselining`: the administrator-only reference baseline is calculated.
- `ready`: question and baseline are complete or baseline is explicitly marked
  unavailable.
- `failed`: provider or validation attempts were exhausted.
- `cancelled`: caller cancellation or a sibling failure stopped the task.
- `persisted`: every planned task was ready and the endpoint saved the complete
  graph.

### Events, guards, and transitions

- `start` moves each indexed task from `planned` to
  `waiting_for_generation_slot`.
- `slot_acquired` moves a task to `generating` only when fewer than two draft
  pipelines own slots globally.
- `provider_response` moves `generating` to `validating`.
- `validation_rejected` moves `validating` to `retrying` when attempts remain;
  otherwise it moves to `failed`.
- `retry_slot_acquired` moves `retrying` back to `generating` within the same
  bounded pipeline.
- `validation_passed` moves `validating` to `generated`.
- `all_generation_passed` starts the baseline phase. This guard prevents
  baseline requests from delaying question generation and prevents baseline work
  for a draft that will be discarded.
- `baseline_slot_acquired` moves a generated task to `baselining`, under the same
  global limit of two.
- `baseline_finished` moves to `ready`; provider-unavailable baseline results are
  stored as unavailable, matching the existing contract.
- `all_ready` moves the ordered graph to `persisted` through one endpoint save.
- `first_failure` cancels queued/in-flight siblings, drains their tasks, preserves
  the original failure, and prevents persistence.
- `caller_cancelled` cancels all queued/in-flight work and prevents persistence.

### Language guards

- Required languages are normalized before prompting.
- Frontend UI tasks require `html`; database tasks require `sql`; REST and bug-fix
  blueprints require `python` and `javascript` by default.
- Single-question generation requires exactly the administrator-requested set.
- Provider-returned languages cannot add, remove, or replace required languages.
- Canonical files are merged before the minimum-file validation.
- Every public and hidden test case must have non-empty test code for every
  required language.
- Workspace runs and final submission pass only files belonging to the selected
  language to the evaluator.
- New REST and bug-fix drafts default to both Python and JavaScript. TypeScript
  remains a legacy backend/runtime value but is not advertised or selectable in
  new admin and student flows.
- Python and JavaScript each contribute seven canonical backend modules. The
  JavaScript schema and environment modules own payload normalization and
  runtime/file-persistence configuration rather than acting as filler files.

### Generated browser-test compatibility

Frontend task checks run in the isolated Jest/jsdom grader. The versioned grader
image provides `JSDOM`, `fake-indexeddb`, and `jest-fetch-mock`; setup exposes
`JSDOM` globally and preloads IndexedDB. A guarded IndexedDB assignment
normalizes the complete `fake-indexeddb` package object to its browser-compatible
factory because older generated checks use that assignment shape. Bumping the
image tag forces Docker to build this dependency contract instead of reusing a
stale cached image.

## Impact surface

- Module 4: draft prompt, retry validation, and provider concurrency.
- Shared prototype / Module 1: canonical JavaScript files, persisted language and
  starter metadata, and final selected-language evaluation.
- Module 2: existing language selector is verified; no provider or database
  access is added, and TypeScript is removed from selectable options.
- Module 3: the baked Jest runtime also supplies generated browser-test DOM,
  IndexedDB, and fetch-mock dependencies; hidden test projection is unchanged.

## Failure and rollback paths

- No database transaction is held during provider work.
- A failed or cancelled blueprint leaves no partial assessment/question graph.
- Existing sequential generation can be restored by setting the concurrency
  constant to one without changing API contracts or stored data.
- Removing JavaScript from a draft remains an administrator edit; existing
  Python, HTML, and SQL contracts are not rewritten.

## Primitive acceptance criteria

- A JavaScript-only REST or bug-fix draft returns at least two non-empty
  JavaScript starter files and JavaScript code in every public and hidden test.
- Default REST and bug-fix blueprints select Python and JavaScript and provide
  seven canonical backend modules for each language.
- TypeScript is absent from new selectable and advertised language options.
- Generated browser checks that instantiate global `JSDOM`, assign the complete
  `fake-indexeddb` module, and require `jest-fetch-mock` run in the sandbox.
- A provider language list that differs from the requested list is rejected and
  retried without changing the requested contract.
- Selecting JavaScript in the student workspace loads JavaScript starter files;
  run and submit evaluate only the selected JavaScript files.
- Two or more blueprint questions exhibit concurrent provider work, never more
  than two draft pipelines at once.
- Completion order does not change task type order, sort order, or score split.
- Any task-generation failure results in no partial persistence.
- Students install no JavaScript dependencies and receive no hidden test content.
