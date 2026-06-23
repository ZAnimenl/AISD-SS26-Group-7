# Task AI-Usage Benchmarks

## Problem

AI-usage grading currently considers the whole attempt without a consistent task-level reference for efficient token use or for the useful context supplied to the agent.

## Decision

Every generated task receives an administrator-only AI-usage benchmark. The platform retains a deterministic reference token budget, recommended interaction count, and four context signals: task goal, active-file/code context, observed behavior or test output, and a desired constraint or acceptance condition.

After generation, the platform also asks the configured provider to summarize the same public task context in a full and compact prompt. From the provider-reported input token counts it records the compression rate and ratio; from the required structured summary fields it records structural utility retention and a reference score. Each valid response also supplies two to five administrator-only standard steps with the least useful AI input and a public verification action. The measured reference is evidence for the AI-usage grader, not a hard token cap or a student-visible standalone grade. If the provider cannot produce a valid baseline, the benchmark records `unavailable` and retains the deterministic fallback rather than inventing measurements.

For a fully passing submitted task, the reference influences up to 15 points of the existing 40-point Token and interaction efficiency criterion. The deterministic score combines compact-reference-relative total-token cost, prompt CpT/TpC, response CpT/TpC, and the required context signals. It is zero when the task goal is unachieved. When no measured task is used, the legacy 30-point semantic behavioral assessment remains unchanged.

## State machine

- `task_generated` -> `benchmark_attached` when the platform derives the deterministic standard from task type and difficulty.
- `benchmark_attached` -> `reference_measuring` when public task context is sent to the configured provider in full and compact forms.
- `reference_measuring` -> `reference_measured` when both provider responses contain valid token counts, required summary fields, and two to five standard steps.
- `reference_measuring` -> `reference_unavailable` when the provider fails or returns invalid evidence; the deterministic reference remains active.
- `reference_measured` or `reference_unavailable` -> `administrator_review` with the generated task.
- `administrator_review` -> `published` only through the existing approval flow.
- `published` -> `attempt_graded` when the AI-usage grader compares task interactions to the stored benchmark and awards deterministic reference-efficiency points only after a fully passing task submission.
- Legacy or manually-authored tasks -> `attempt_graded` with a deterministic fallback benchmark; no migration is required.

## Security and rollback

Benchmarks live in grading configuration and are returned only through administrator task APIs. Student workspace payloads, agent prompts, hidden tests, provider credentials, and server prompts remain unchanged. Removing the benchmark reader returns grading to its existing attempt-level evidence without changing stored task content.

## Primitive acceptance criteria

- A generated task includes a versioned deterministic benchmark derived from its type and difficulty.
- When provider measurement succeeds, the benchmark records full and compact token counts, compression rate and ratio, structural utility retention, a reference score, and two to five minimal-input standard steps with public verification.
- When provider measurement fails, the benchmark explicitly records an unavailable measured reference and retains deterministic fallback values.
- Grading receives a per-task actual-versus-benchmark summary without treating low token use alone as success; task-goal achievement, density, context, and reference-relative cost jointly determine the bounded deterministic contribution.
- Students cannot retrieve benchmark metadata from the workspace API.
