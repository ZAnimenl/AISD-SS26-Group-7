# Automatic AI Usage Scoring and Reflection

## Status

Approved target design for implementation. The repository does not yet
implement the reflection workflow, suggestion decision telemetry, or automatic
AI Usage Score described here.

## Problem Definition

The platform currently reports functional test scores and descriptive AI token
metrics, but it does not produce a defensible AI Usage Score. A fixed token
threshold is not suitable because token consumption varies with model,
tokenizer, task complexity, response length, and automatically supplied
workspace context.

AI-enabled assessments require a separate automatic score for the quality of
the student's AI-assisted workflow. AI-disabled assessments retain functional
grading only.

## Product Decision

For an AI-enabled assessment, the student receives:

- Functional Score: `0-100`, calculated from automated code evaluation.
- AI Usage Score: `0-100`, calculated from interaction telemetry and the
  required reflection.
- Final Score: `(Functional Score + AI Usage Score) / 2`.

For an AI-disabled assessment, the student receives only the Functional Score.
No AI Usage Score, reflection, or averaged Final Score is produced.

The administrator's assessment-level `AI enabled` setting selects the complete
workflow. When AI is enabled, use of the platform AI agent and completion of the
reflection workflow are mandatory. At least one successfully logged AI
interaction must exist before the student can begin final submission.

## Options Considered

### Fixed absolute token threshold

Rejected. A universal threshold such as 2,500 tokens has no project-specific
calibration and is not comparable across tasks, models, tokenizers, or context
sizes.

### Cohort-relative token grading

Rejected for the initial scoring model. The score must not depend on when other
students submit or on the behavior of a particular cohort.

### Unstructured LLM judgment

Rejected. Asking an LLM whether usage was "efficient" without a fixed rubric,
event timeline, and evidence requirements is too subjective and difficult to
audit.

### Structured hybrid automatic grading

Selected. Deterministic event metrics cover measurable repetition and rapid
unchanged acceptance. A fixed LLM rubric evaluates semantic qualities that
cannot be measured from counts alone. Every criterion must cite evidence from
the stored attempt timeline.

## AI Usage Score

| Criterion | Maximum |
| --- | ---: |
| Prompt quality and context | 30 |
| Token and interaction efficiency | 40 |
| Critical evaluation and adaptation | 20 |
| Reflection quality and consistency | 10 |
| Total | 100 |

Raw token count is evidence, not a direct penalty. The score has no absolute
token cutoff and no cohort-relative component.

### Prompt quality and context - 30 points

The grading LLM evaluates:

- task relevance and specificity: 10 points
- useful code, error, run-result, or requirement context: 10 points
- iterative refinement based on prior responses and observed results: 10 points

Vague prompts, repeated complete-solution requests, irrelevant requests, and
prompts that ignore already available context reduce this score.

### Token and interaction efficiency - 40 points

#### LLM behavioral efficiency - 30 points

The grading LLM evaluates:

- productive conversion of responses into relevant progress: 12 points
- workflow economy, including using or evaluating a response before requesting
  more assistance: 8 points
- efficient recovery from errors through targeted evidence and changed
  strategy: 5 points
- justification of token-heavy interactions by task complexity, multi-file
  work, or difficult debugging: 5 points

The evaluator judges the sequence:

`prompt -> response -> action -> code change -> run/test -> next prompt`

It must not award efficiency merely because token usage is low, and it must not
penalize a long interaction when the evidence shows that the interaction was
necessary and productive.

#### Objective repetition metrics - 10 points

This component starts at 10 points. Deterministic analysis may deduct:

- up to 4 points for near-duplicate prompts without an intervening student
  action, code change, or run
- up to 3 points for requesting regeneration or another answer before
  evaluating the previous response
- up to 3 points for repeating the same error-request cycle without changing
  evidence or strategy

Semantic similarity may identify candidate repetitions, but the deduction must
also consider intervening timeline events. Exact-text comparison alone is
insufficient.

### Critical evaluation and adaptation - 20 points

The grading LLM evaluates whether the student:

- adapted, corrected, rejected, or selectively applied AI suggestions
- used run results, tests, previews, or code inspection to evaluate suggestions
- asked critical follow-up questions or compared alternatives
- demonstrated independent judgment rather than automatically accepting every
  response

For each actionable code or workspace suggestion, the client records when the
response becomes visible and when the student applies, edits, rejects,
dismisses, or undoes it.

Applying an actionable suggestion unchanged within three seconds is a
`rapid_unchanged_accept` event. Each such event deducts one point from this
criterion, up to a maximum deduction of eight points.

The rapid-accept rule is evidence, not proof of misconduct:

- explanatory responses and trivial non-code actions are excluded
- an immediate undo cancels the deduction
- a substantial edit made as part of application or immediately afterward
  cancels the deduction
- waiting longer than three seconds does not itself earn points

The timer starts when the complete actionable response is rendered to the
student, not when provider generation starts or ends. The client records a
monotonic elapsed duration, while the backend records receipt timestamps.
Client timing is treated as auditable telemetry, not tamper-proof evidence.

### Reflection quality and consistency - 10 points

The reflection prompt is:

> In no more than 100 words, explain how you used the AI assistant during this
> assessment. Include one suggestion that helped and how you verified it, and
> one suggestion that you rejected, corrected, or found unhelpful.

The grading LLM evaluates:

- coverage of all three requested elements
- specificity and clarity
- consistency with prompts, responses, suggestion actions, code changes, and
  execution logs

Contradictions reduce the score. A fabricated or materially contradictory
reflection receives zero points for this criterion. The reflection cannot
override contradictory platform telemetry.

## Submission and Reflection State Machine

### States

- `active`: code and AI interaction are available.
- `code_submitting`: the final workspace is being frozen and functionally
  evaluated.
- `reflection_pending`: code is frozen and the ten-minute reflection timer is
  active.
- `ai_grading_pending`: reflection is final and automatic AI grading is queued
  or running.
- `completed`: all required scores are available.
- `ai_grading_failed`: functional submission remains valid, but the AI score
  requires an automatic retry or administrator-visible resolution.

AI-disabled assessments transition from `code_submitting` directly to
`completed` after functional grading.

### Events and transitions

- Confirm final submission:
  - guard: active attempt; for AI-enabled assessments, at least one successfully
    logged AI interaction exists
  - effect: freeze workspace and begin functional evaluation
- Functional submission accepted:
  - AI enabled: enter `reflection_pending` and start the backend-authoritative
    ten-minute deadline
  - AI disabled: enter `completed`
- Reflection submitted early:
  - freeze the reflection and enter `ai_grading_pending`
- Reflection deadline reached:
  - autosubmit the latest saved reflection, including an empty reflection, and
    enter `ai_grading_pending`
- AI grading succeeds:
  - persist rubric version, model identifier, criterion scores, evidence,
    summary, and final score; enter `completed`
- AI grading provider or schema failure:
  - preserve functional score and reflection; enter `ai_grading_failed`
  - never substitute zero for infrastructure failure

## Reflection Interaction Rules

- The reflection is shown only for AI-enabled assessments.
- The 10-minute timer starts only after the backend confirms that final code is
  frozen.
- The reflection is limited to 100 words.
- A live word count and remaining time are visible.
- The draft is autosaved.
- Code editing, Run, AI assistance, and resubmission remain unavailable while
  reflection is pending.
- The latest saved draft is submitted automatically at timeout.
- An empty timeout reflection receives zero of 10 reflection points, but does
  not invalidate the functional submission.
- Refreshing or reconnecting restores the backend-owned deadline and latest
  reflection draft; it does not restart the timer.

## Required Telemetry

The scoring timeline requires:

- AI interaction ID, prompt, response, interaction type, semantic tags, task,
  timestamps, and input/output token counts
- response-visible event
- suggestion apply, edit-before-apply, reject, dismiss, and undo events
- elapsed decision time
- whether applied content was unchanged or substantially modified
- workspace file revisions linked to AI actions where possible
- public run and preview events and their results
- final functional results
- reflection drafts, final reflection, deadline, and submission reason
  (`student_submit` or `timeout`)

## Automatic Grader Contract

The grader uses a fixed, versioned rubric and returns structured output:

```json
{
  "rubric_version": "ai-usage-v1",
  "model": "configured-grading-model",
  "ai_usage_score": 78,
  "criteria": {
    "prompt_quality_and_context": 23,
    "behavioral_efficiency": 22,
    "objective_repetition": 8,
    "critical_evaluation_and_adaptation": 17,
    "reflection_quality_and_consistency": 8
  },
  "reflection_consistency": "mostly_consistent",
  "confidence": "medium",
  "summary": "The student used targeted debugging prompts and verified most suggestions.",
  "evidence": [
    {
      "criterion": "behavioral_efficiency",
      "interaction_id": "uuid",
      "finding": "The student used the response before requesting further help."
    }
  ]
}
```

The backend validates score ranges and checks that the weighted components sum
to the returned AI Usage Score. The deterministic repetition score and
rapid-accept deductions are calculated by platform logic and supplied to the
LLM as fixed evidence; the LLM cannot overwrite those measurements.

## Reliability and Calibration

LLM evaluation is useful for semantic rubric application but is not assumed to
be inherently objective. Before graded deployment, course staff should compare
automatic criterion scores with manually scored representative attempts and
revise the rubric prompt until acceptable agreement is achieved.

The production grader must:

- use a fixed model configuration and rubric version
- use deterministic or low-variance generation settings
- require criterion-level evidence
- store the complete grading input manifest and structured result
- reject malformed or out-of-range output
- retry provider and schema failures without changing the student's score
- expose low-confidence and failed grading states to administrators

Relevant research supports analyzing programming-student interaction
trajectories such as delegation, iterative refinement, and repeated repair
cycles, but it does not establish a universal educational token threshold:

- Rahe and Maalej, "How Do Programming Students Use Generative AI?"
  <https://arxiv.org/abs/2501.10091>
- Shao et al., "Tracing Prompt-Level Trajectories to Understand Student
  Learning with AI in Programming Education"
  <https://arxiv.org/abs/2604.10400>
- Hashemi et al., "LLM-RUBRIC: A Multidimensional, Calibrated Approach to
  Automated Evaluation of Natural Language Texts"
  <https://aclanthology.org/2024.acl-long.745/>
- Lee, Hong, and Thorne, "Evaluating the Consistency of LLM Evaluators"
  <https://aclanthology.org/2025.coling-main.710/>

## Impact Surface

- Module 1: assessment settings, attempt state, reflection persistence, score
  persistence, reports, and grading audit data
- Module 2: submission transition, reflection UI, timers, word count, suggestion
  event telemetry, results, and report presentation
- Module 3: functional score remains independent and unchanged
- Module 4: interaction logging, automatic grading prompt, rubric application,
  token analytics, evidence generation, and provider failure handling

## Rollback

The automatic AI grading mechanism can be disabled by configuration while
preserving raw telemetry, reflections, and functional scores. Existing
AI-disabled assessments remain unaffected. Stored rubric versions allow scores
to be audited or recalculated after a rubric update without changing the
original record.

## Open Implementation Decisions

- Define the deterministic threshold for a "substantial edit" that cancels a
  rapid-accept deduction. The implementation should prefer normalized code or
  syntax-aware change measurement over raw character count.
- Define the assessment policy when AI is enabled but the provider remains
  unavailable before the student can create the required first interaction.
  The current product rule blocks final submission until one interaction is
  logged; an outage exception would require an explicit requirement change.
- Decide whether administrators may only inspect and retry automatic grading or
  may also override the AI Usage Score. Any override would require actor,
  timestamp, reason, original score, and replacement score audit fields.
- Select and calibrate the grading model against a manually scored reference
  set before the AI Usage Score contributes to real course grades.
- Define word counting for the 100-word reflection consistently between
  frontend and backend, including punctuation, hyphenated terms, and pasted
  code.

## Primitive Acceptance Criteria

- AI-disabled assessments produce only a Functional Score.
- AI-enabled assessments require logged platform AI use and a timed reflection
  workflow.
- Functional and AI Usage scores are each represented on a 0-100 scale.
- The Final Score for AI-enabled assessments is their arithmetic mean.
- AI Usage Score weights are 30/40/20/10 as defined above.
- No fixed token threshold or cohort-relative token score affects grading.
- Rapid unchanged acceptance within three seconds is recorded and applies only
  the bounded critical-evaluation deduction.
- Reflection is limited to 100 words and finalized within a backend-owned
  ten-minute window.
- Provider failure leaves AI grading pending or failed; it never assigns a
  fabricated score or zero.
- Every automatic score is versioned and accompanied by criterion-level
  evidence.
