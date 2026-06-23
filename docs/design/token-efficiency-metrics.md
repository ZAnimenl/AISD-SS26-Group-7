# Token-Efficiency Metrics and Reference Baseline

## Problem definition

The workspace records provider token counts, but students and administrators
cannot distinguish token density, compact context use, or an evidence-backed
reference baseline. A generated assessment therefore needs a real, provider
measured reference exercise after generation, while the student-facing AI panel
needs compact metrics that do not expose hidden tests or grading configuration.

## Research basis

- Kumar, *Is Sanskrit the Most Token-Efficient Language?* (arXiv:2601.06142)
  defines characters per token (CpT) as information density and tokens per
  character (TpC) as representation cost. The platform uses Unicode scalar
  characters rather than UTF-16 code units.
- Jiang et al., *LLMLingua* (EMNLP 2023) defines compression rate
  `tau = compressed_tokens / original_tokens` and compression ratio
  `CR = 1 / tau`. Its objective balances compression with retained utility.
- Wang et al., *Thoughts Are All Over the Place* (arXiv:2501.18585) defines
  underthinking for incorrect answers as the average of
  `1 - first_correct_thought_tokens / total_response_tokens`. That quantity
  requires a verified first correct thought and cannot be inferred from an
  opaque provider response.
- Liang et al., *GenericAgent* (arXiv:2604.17091) motivates tracking complete,
  decision-relevant context rather than raw context length alone.

The source papers:

- https://arxiv.org/abs/2601.06142
- https://aclanthology.org/2023.emnlp-main.825/
- https://arxiv.org/abs/2501.18585
- https://arxiv.org/abs/2604.17091

## Metrics

For a text with `C` Unicode scalar characters and provider-reported `T`
tokens:

- `CpT = C / T` when `T > 0`, otherwise `0`.
- `TpC = T / C` when `C > 0`, otherwise `0`.

The workspace exposes prompt-source and response CpT/TpC separately. Prompt
source consists only of the student message and active-file content stored with
the interaction; it does not imply that hidden or server-side prompt text was
shown to the student.

For every generated question, the baseline runner sends two real provider
requests with the same structured utility task. Each response also contains an
administrator-only `standard_steps` draft: two to five steps, each with the
minimal useful AI input and a public verification action. It is generated from
public task text and visible starter files only, is available during
administrator review, and never contains hidden tests or grading instructions.

The runner makes two requests:

1. a complete public-context prompt containing the task and visible starter
   files;
2. a compact, information-complete prompt containing the task goal, one code
   context summary, an explicit no-run-yet observation, and the constraints.

With provider-reported input token counts `L_full` and `L_compact`:

- `tau = L_compact / L_full`;
- `CR = L_full / L_compact`.

Each response must return all four required context fields. Structural utility
retention is `min(full_fields, compact_fields) / 4`. The platform reference
score is `100 * (1 - tau) * structural_utility_retention`. It is a transparent
platform baseline, **not** a student grade and not a claim of semantic
equivalence or global theoretical optimality.

The paper's underthinking score is deliberately omitted. It requires a verified
first correct thought in an incorrect response; showing a proxy would misstate
the research and require private chain-of-thought data.

## Graded reference-efficiency component

The Token and interaction efficiency criterion remains `0-40`: an LLM grades
semantic behavior and deterministic repetition supplies `0-10`. For a task
that has a completed reference baseline and at least one logged interaction,
the semantic behavioral portion is `0-15` and the deterministic
reference-efficiency portion is `0-15`. If no measured task was used, the
legacy semantic behavioral portion remains `0-30`, so an unavailable provider
cannot lower a student score.

The deterministic portion is available only when the student's submitted task
achieves its goal (full automated-submission score). For each measured task:

- `context = provided_context_signals / 4`.
- `cost = min(1, compact_reference_total_tokens / student_total_tokens)`.
- `prompt_density` is the geometric mean of the bounded CpT comparison
  `min(student_CpT / reference_CpT, 1)` and the bounded TpC comparison
  `min(reference_TpC / student_TpC, 1)`.
- `response_density` applies the same CpT/TpC comparison to the student AI
  response and the compact reference response.
- `task_score = round(15 * (0.35 * cost + 0.25 * context + 0.20 *
  prompt_density + 0.20 * response_density))`.

The reference-efficiency score is the arithmetic mean of the measured tasks
the student used. CpT and TpC are both recorded for transparency but are
reciprocal representations of the same density, so the geometric mean prevents
double-counting. This is a reference-relative measurement, not an absolute
token threshold or cross-cohort comparison. It cannot award efficiency points
to an unachieved task goal.

## Option comparison

- Use a fixed token budget by task type: retained only as a legacy fallback;
  it is not a provider-measured baseline.
- Estimate tokens from characters: rejected for the reference baseline because
  tokenizer behavior is model-dependent.
- Run matched full and compact provider prompts: selected; it uses the active
  provider's actual token accounting and preserves a comparable utility task.
- Expose baseline internals to students: rejected because benchmark metadata is
  administrator-only and must not reveal grading implementation.

## State machine

- States: `draft_generated`, `baseline_running`, `baseline_complete`,
  `baseline_unavailable`, `task_goal_unachieved`, `reference_scored`.
- Events: generated question validated; full reference completes; compact
  reference completes; provider becomes unavailable; compact result fails the
  structured utility check; student submits a fully passing task; grading runs.
- Guards: only generated questions run a baseline; only public task text and
  starter files enter the baseline; a completed baseline requires positive
  provider input token counts and all four utility fields in both results.
- Transitions: validated generated task moves to `baseline_running`; two valid
  provider results with valid standard steps move to `baseline_complete`; a
  provider failure or invalid result moves to `baseline_unavailable`. During
  grading, a measured task with a full automated-submission score moves to
  `reference_scored`; otherwise it moves to `task_goal_unachieved` and receives
  zero reference-efficiency points.
- Side effects: store only counts, ratios, density references, standard-step
  drafts, coverage, and status in the administrator-only grading configuration.
  Never store hidden test content, grading implementation, or a provider
  response beyond the vetted standard-step draft.
- Failure path: preserve the generated draft and record an unavailable baseline;
  do not substitute estimated tokens as if they were provider measurements.
- Rollback: remove the baseline metadata key and restore the prior deterministic
  benchmark reader. Existing configurations continue to use the legacy fallback.

## Impact surface

- Module 4 provider calls and generated-task metadata.
- Module 1 assessment-generation and student AI-usage projection.
- Module 2 AI rail metrics display and administrator question review.
- Existing `ai_usage_benchmark` metadata remains administrator-only.

## Primitive acceptance criteria

- A generated question records a completed reference baseline only after the
  provider has measured both complete and compact public-context prompts and
  returned two to five valid standard steps.
- The stored compression rate and ratio equal the recorded input-token counts.
- A failed baseline is visibly marked unavailable to administrators and is never
  presented as estimated or provider-measured.
- The student AI rail displays only the active task's observable density and
  context-coverage metrics; it does not expose hidden tests, baseline prompts,
  or grading configuration.
- A completed, fully passing task can receive deterministic reference-efficiency
  points from CpT, TpC, context coverage, response density, and
  compact-reference-relative cost; an unachieved task goal cannot receive them.
- A missing or unavailable baseline cannot lower the student's behavioral
  efficiency score.
- The system does not label any proxy as the underthinking score from the paper.
