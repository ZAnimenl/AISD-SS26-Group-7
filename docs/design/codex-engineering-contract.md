# Codex Engineering Contract Adoption

## Problem Definition

The repository had an agent orchestration file that mixed project routing,
skills, module boundaries, and workflow details. The owner now requires a
stricter 16-point engineering contract as the active operating rule for coding
agents while preserving the previous GitHub `AGENTS.md` for reference.

## Option Comparison

### Option A: Keep the previous GitHub `AGENTS.md`

- Pros: no file movement and no workflow churn.
- Cons: does not satisfy the owner request and does not make the 16-point
  contract active.

### Option B: Replace `AGENTS.md` and discard the previous file

- Pros: makes the new contract active.
- Cons: loses historical project routing context and is harder to roll back.

### Option C: Replace `AGENTS.md` and archive the previous file

- Pros: makes the new contract active, preserves rollback context, and keeps
  the change reversible.
- Cons: requires future agents to treat the archive as historical only.

Chosen option: Option C.

## State Machine

States:

- `legacy-active`: previous GitHub `AGENTS.md` is active.
- `archived`: previous file is saved under `docs/archived-github-agents.md`.
- `contract-active`: root `AGENTS.md` contains the 16-point engineering
  contract plus repository operating supplement.
- `verified`: documentation and automated checks have passed or blockers are
  reported.
- `rolled-back`: archived file is restored to root if the owner requests it.

Events and transitions:

- `owner-requests-contract`: `legacy-active` to `archived`.
- `archive-complete`: `archived` to `contract-active`.
- `checks-pass`: `contract-active` to `verified`.
- `checks-fail`: `contract-active` remains active, with blockers reported.
- `owner-restores-legacy`: `contract-active` or `verified` to `rolled-back`.

Guards:

- The previous file must be preserved before replacement.
- Root `AGENTS.md` must remain the only active repository instruction file.
- Specification files must not be edited for this governance change.

Side effects:

- Future agent behavior follows the engineering contract.
- Minimal project documents are created where missing.
- `ACCEPTANCE.md` records the active governance acceptance criteria.

Failure paths:

- If archive creation fails, do not replace root `AGENTS.md`.
- If checks fail, do not deploy silently.
- If branch or push permissions fail, keep the local commit and report the
  blocker.

Rollback path:

- Restore `docs/archived-github-agents.md` to `AGENTS.md`.
- Remove the contract-specific acceptance criteria if the owner no longer wants
  them.
- Keep product documents only if the owner accepts them as useful project
  documentation.

## Impact Surface

- Affects coding-agent instructions and future repository governance.
- Affects documentation inventory and acceptance criteria.
- Does not change application runtime code, database schema, API contracts,
  authentication, sandbox behavior, or deployment configuration.

## Primitive Acceptance Criteria

- The previous GitHub `AGENTS.md` content exists at
  `docs/archived-github-agents.md`.
- Root `AGENTS.md` states that the archived file is historical context only.
- Root `AGENTS.md` contains the 16 numbered engineering contract sections.
- Minimal BRD, MRD, PRD, TRD, DesignSpec, TestCase, and Acceptance documents
  exist.
- Verification commands are run and results are reported before push or deploy.
