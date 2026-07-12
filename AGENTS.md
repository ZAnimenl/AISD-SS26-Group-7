# Codex Engineering Contract

This is the active repository-level instruction file for coding agents.

The previous GitHub `AGENTS.md` has been preserved at
`docs/archived-github-agents.md`. Treat that archived file as historical
context only. It is not the active instruction source unless the repository
owner explicitly restores it.

## 0. Mission

You are a software engineering agent. Deliver changes that are small,
verifiable, reversible, and loosely coupled. Do not write speculative code for
unconfirmed future needs.

When a request is unclear, challenge it constructively. You are expected to add
necessary engineering friction rather than blindly implement requests that would
make the project inconsistent or hard to maintain, unless the owner explicitly
tells you not to challenge the request.

## 1. Task Protocol

For every task, first identify:

- Goal
- Context
- Constraints
- Done when

For complex or ambiguous tasks, plan first. Ask when ambiguity blocks progress.
For non-blocking assumptions, state the assumption explicitly.

## 2. Project Documents

Each project must maintain:

- BRD
- MRD
- PRD
- TRD
- DesignSpec
- TestCase
- Acceptance

Project documents live under `docs/`. Final acceptance criteria live in the
root `ACCEPTANCE.md`.

When requirements, behavior, interfaces, state machines, or acceptance criteria
change, update the relevant documents in the same change.

## 3. Research Discipline

When work involves a new mechanism, architecture, external API, dependency,
standard, or security boundary, research official or authoritative sources
first. Record the basis in the relevant design document. If network access is
unavailable, state that limitation.

## 4. Scope Discipline

Implement only what is explicitly required and what acceptance criteria require.
Do not add parameters, configuration, extension points, abstractions, or
dependencies because they might be useful later.

When two options are functionally equivalent, choose the more reversible option.

## 5. Architecture Principles

Decoupling is an engineering prerequisite, but abstractions must serve current
boundaries.

Use interfaces or ports across module boundaries and for volatile dependencies.
Inside a stable module implementation, concrete classes are acceptable.

Each module must have a responsibility that can be described in one unambiguous
sentence. If that sentence needs "and", "also", or equivalent mixed ownership,
split the responsibility.

State must be held by upper layers or injected explicitly. Side effects belong
at boundary layers and must be covered by behavior tests.

Core modules must not instantiate external collaborators directly. Inject
collaborators through constructors, factories, or function parameters.

Prefer interfaces plus composition for reuse. Use inheritance only for stable
data type hierarchies or framework-required type structures.

Keep production-facing surface area as small as practical so replacements,
upgrades, and rollbacks remain easy.

## 6. State-Machine First

Before designing a new mechanism, workflow, agent collaboration, async task,
permission flow, approval flow, UI state, or external protocol, write the state
machine first:

- states
- events
- guards
- transitions
- side effects
- failure paths
- rollback paths

## 7. Before Editing

Before editing existing code, state the impact surface in one sentence:

- callers
- dependencies
- data
- permissions
- deployment paths

If the impact surface exceeds the task boundary, pause and report it.

## 8. Implementation Loop

Move in the smallest testable unit:

1. Implement.
2. Run targeted checks.
3. Review decoupling.
4. Fix known issues.
5. Continue.

The decoupling review must check:

- mixed responsibilities
- concrete dependencies leaking into boundaries
- hidden state or hidden side effects
- new logic attached to a god object
- names that still carry old semantics
- dead code, commented-out code, or ownerless TODOs
- drift from `ACCEPTANCE.md`

Do not continue while known coupling issues, failing tests, or acceptance drift
remain in the touched scope.

## 9. Hard-Stop Signals

Stop and split or report when any of these appear:

- a single class or file exceeds 1000 lines
- constructor parameters exceed 6
- a function, class, or module name contains `And` or another word meaning mixed
  ownership
- a boundary module directly imports another module's concrete implementation
- a function creates hidden side effects
- a new requirement has no acceptance criteria
- a change needs to expand outside the task area

## 10. Legacy Code

Do not preserve coupling in the touched scope just because a minimal edit would
be easier. When touching legacy code, split the coupling point involved in the
current change.

When a requirement change makes logic unused, delete the dead code immediately.
Do not keep it as comments or leave removal notes.

When a function or module responsibility changes, update its name.

## 11. Testing

Test behavior contracts, not private methods, internal call order, or accidental
implementation details.

Match test scope to change size:

- small change: targeted tests
- medium change: related integration, type, and lint checks
- release or cross-boundary change: key end-to-end checks

Failed tests must be diagnosed and fixed. If they cannot be fixed in the task,
report the failing command, root cause, impact, and next step.

## 12. New Mechanism Requirement

When a request introduces a new mechanism, architecture, or design rule, create
`docs/design/<topic>.md` first.

That design document must include:

- problem definition
- option comparison
- state machine
- impact surface
- rollback path
- primitive acceptance criteria

Primitive acceptance criteria describe user-observable facts, system state
changes, and boundary behavior. They must not depend on framework, class, or
implementation details.

## 13. Review and Delivery

Before finishing, review the diff and confirm:

- functional requirements are satisfied
- non-functional requirements are satisfied
- acceptance criteria are updated or still applicable
- documents are synchronized
- tests were run at the right scale
- there is no dead code or naming drift
- the impact surface did not expand unintentionally

Final replies must include:

- change summary
- test commands and results
- documentation updates
- risks or remaining issues
- completion percentage

The completion percentage must be based on `ACCEPTANCE.md`. If no quantitative
standard exists, label it as an estimate.

## 14. Remote and Deploy

When the owner has not forbidden it and the project is marked deployable, push
and deploy after tests pass so the owner can verify the real result.

Do not deploy silently when:

- credentials, permissions, or environment variables are missing
- tests fail
- production-destructive migration is involved
- the impact surface exceeds the task boundary
- security, privacy, or permission risk remains
- the deployment target is unclear

In those cases, keep a local diff, patch, commit, or pull request and report the
blocker clearly.

## 15. Subagents

Use subagents explicitly for complex work when parallel analysis would improve
depth. Suitable parallel work includes repository exploration, test-gap
analysis, log analysis, security review, documentation review, and acceptance
review.

Code writing defaults to a single main line of execution. Avoid multiple agents
writing at once. If parallel implementation is required, split work into
non-overlapping files or modules, then let the main agent merge and review.

When subagents are used, wait for all results, summarize them as decision
inputs, and only then continue.

## 16. No Mock Delivery

At no stage, including intermediate stages, may a mock replace a real
deployment, real function, or real data as the delivered outcome.

Mocks may be used only for tests. After testing, replace test mocks with the
real implementation in the project body before claiming delivery.

# Project Operating Supplement

This supplement binds the engineering contract to this repository. It is
derived from the active project specifications, not from the archived GitHub
agent file.

## Authoritative Documents

Use these documents in priority order when product or implementation choices
conflict:

1. `SPEC.md`
2. `(English Ver.) Architectural Design and Module Specification for an AI-Assisted Online Coding Assessment Platform.pdf`
3. `complete_frontend_api_list_and_backend_alignment.md`
4. `ui-style-reference.md`

Do not edit those four specification files unless the owner explicitly asks for
specification changes. If implementation conflicts with them, stop and report
the conflict before editing implementation files.

## Module Ownership

Module 1, Identity and Assessment Management, owns authentication, RBAC, users,
assessments, tasks, test cases, attempts, workspace persistence, submissions,
results, reports, EF Core persistence, local SQLite startup, and PostgreSQL
deployment persistence.

Module 2, Interactive Browser-Based Workspace and Frontend IDE, owns Next.js UI,
student and admin pages, Monaco/editor behavior, workspace UI, preview and
verification UI, frontend API clients, loading states, error states, and visual
polish.

Module 3, Sandboxed Code Execution and Evaluation Engine, owns isolated
execution, resource limits, stdout and stderr capture, hidden-test evaluation,
execution result schemas, worker lifecycle, and cleanup.

Module 4, AI Telemetry and Assistance Service, owns the secure AI backend,
provider calls, server-side prompts, interaction logging, token tracking,
semantic tags, structured AI responses, and provider error handling.

Use cross-module integration ownership only when a task requires coordinated
frontend/backend/API/auth/data-flow work.

## Security Boundaries

Student-facing UI and APIs must never expose hidden test inputs, hidden expected
outputs, grading implementation, or administrator-only notes.

The frontend must not access the database directly, call the sandbox directly,
call external AI providers directly, execute student code locally, or own real
attempt resolution.

Untrusted student code must execute only through the sandboxed evaluation
architecture.

AI provider keys, server-side system prompts, hidden tests, grading criteria,
and provider request details must remain server-side.

## Session and Attempt Rule

Current backend-connected assessment flows are assessment-scoped. The frontend
must not create, store, trust, or send a real `session_id` or `attempt_id`.

The backend resolves the active attempt from authenticated user context plus
`assessment_id`.

If a task appears to require frontend-managed `session_id`, stop and ask for a
decision.

## Shared Prototype Assessment Rule

The shared runnable prototype is platform-managed starter content. Students
must be able to work inside the assessment website without installing local
dependencies.

The first implementation supports four practical task categories:

- frontend UI extension
- REST API development
- database query/schema work
- bug fixing in existing code

Only frontend UI extension tasks require direct browser UI preview. Other task
types may use API output, database result tables, automated tests, or regression
test output.

LLM-generated tasks and tests are drafts until an administrator reviews and
approves them.

## Local Skill Usage

Local skills under `.agents/skills/` are reusable role checklists. They are not
separate sources of project truth.

For implementation work, use this sequence unless the owner requests planning
only or review only:

1. Use `prompt-commander` to route scope and risks.
2. Activate only the skills that match the task.
3. Implement with one primary coder skill unless the work is truly cross-module.
4. Use companion skills only for boundary checks.
5. Finish with `strict-code-reviewer`.

Use `handoff-summary` when work is paused, blocked, or context is near the
limit.
