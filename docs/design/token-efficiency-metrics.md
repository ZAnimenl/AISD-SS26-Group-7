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
requests with the same structured utility task:

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
  `baseline_unavailable`.
- Events: generated question validated; full reference completes; compact
  reference completes; provider becomes unavailable; compact result fails the
  structured utility check.
- Guards: only generated questions run a baseline; only public task text and
  starter files enter the baseline; a completed baseline requires positive
  provider input token counts and all four utility fields in both results.
- Transitions: validated generated task moves to `baseline_running`; two valid
  provider results move to `baseline_complete`; a provider failure or invalid
  result moves to `baseline_unavailable`.
- Side effects: store only counts, ratios, coverage, and status in the
  administrator-only grading configuration. Never store the provider response
  or hidden test content.
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
  provider has measured both complete and compact public-context prompts.
- The stored compression rate and ratio equal the recorded input-token counts.
- A failed baseline is visibly marked unavailable to administrators and is never
  presented as estimated or provider-measured.
- The student AI rail displays only the active task's observable density and
  context-coverage metrics; it does not expose hidden tests, baseline prompts,
  or grading configuration.
- The system does not label any proxy as the underthinking score from the paper.
