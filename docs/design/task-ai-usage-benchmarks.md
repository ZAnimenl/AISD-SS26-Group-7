# Task AI-Usage Benchmarks

## Problem

AI-usage grading currently considers the whole attempt without a consistent task-level reference for efficient token use or for the useful context supplied to the agent.

## Decision

Every generated task receives a deterministic, administrator-only AI-usage benchmark. It defines a reference token budget, a recommended interaction count, and four context signals: task goal, active-file/code context, observed behavior or test output, and a desired constraint or acceptance condition. The benchmark is reference evidence for the AI-usage grader, not a hard token cap.

## State machine

- `task_generated` -> `benchmark_attached` when the platform derives the standard from task type and difficulty.
- `benchmark_attached` -> `administrator_review` with the generated task.
- `administrator_review` -> `published` only through the existing approval flow.
- `published` -> `attempt_graded` when the AI-usage grader compares task interactions to the stored benchmark.
- Legacy or manually-authored tasks -> `attempt_graded` with a deterministic fallback benchmark; no migration is required.

## Security and rollback

Benchmarks live in grading configuration and are returned only through administrator task APIs. Student workspace payloads, agent prompts, hidden tests, provider credentials, and server prompts remain unchanged. Removing the benchmark reader returns grading to its existing attempt-level evidence without changing stored task content.

## Primitive acceptance criteria

- A generated task includes a versioned benchmark derived from its type and difficulty.
- The benchmark records token-efficiency references and required context signals.
- Grading receives a per-task actual-versus-benchmark summary without treating low token use alone as success.
- Students cannot retrieve benchmark metadata from the workspace API.
