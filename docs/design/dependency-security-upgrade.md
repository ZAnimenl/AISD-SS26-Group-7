# Dependency Security Upgrade

## Problem Definition

Repository verification found root frontend dependency advisories in `npm audit`.
The remaining vulnerable paths were tied to the Next.js and ESLint toolchain,
Monaco editor transitive dependencies, and PostCSS. Leaving those advisories in
place would make deployment unsafe under the active engineering contract.

## Option Comparison

### Option A: Keep the existing dependency stack

- Pros: no migration risk.
- Cons: leaves high and moderate dependency advisories unresolved.

### Option B: Run `npm audit fix --force` blindly

- Pros: follows npm's automated remediation path.
- Cons: creates an ESLint peer-version conflict and does not preserve a verified
  lint command.

### Option C: Apply targeted dependency and tooling migration

- Pros: removes audit findings while keeping the migration explicit and
  testable.
- Cons: requires updating the lint command and ESLint flat config.

Chosen option: Option C.

## Research Basis

- Next.js official ESLint documentation for version 16 states that `next lint`
  was removed and ESLint should be run through the ESLint CLI:
  `https://nextjs.org/docs/app/api-reference/config/eslint`.
- Next.js official version 16 upgrade guide records the flat-config ESLint
  migration and removal of the Next config `eslint` option:
  `https://nextjs.org/docs/app/guides/upgrading/version-16`.
- The final dependency state is verified by local `npm audit --audit-level=moderate`
  rather than by assuming package advisories are fixed.

## State Machine

States:

- `vulnerable`: `npm audit --audit-level=moderate` reports findings.
- `migrating`: dependency versions and tool configs are being updated.
- `verified`: audit, typecheck, lint, build, and backend tests pass.
- `rolled-back`: package and tooling files are restored to the previous stack.

Events and transitions:

- `audit-fails`: `vulnerable` remains active.
- `dependencies-updated`: `vulnerable` to `migrating`.
- `checks-pass`: `migrating` to `verified`.
- `checks-fail`: `migrating` remains active until fixed or rolled back.
- `rollback-requested`: `migrating` or `verified` to `rolled-back`.

Guards:

- Do not change application behavior to satisfy dependency tooling.
- Do not silence lint rules when a small behavior-preserving code fix is
  available.
- Do not deploy unless audit and build/test gates pass.

Side effects:

- Next.js and `eslint-config-next` move to `16.2.7`.
- ESLint moves to a compatible `9.x` release and uses flat config.
- Monaco editor is explicitly pinned to `0.53.0` to avoid the vulnerable
  DOMPurify transitive path.
- PostCSS is pinned and overridden to `8.5.15`.

Failure paths:

- If the upgraded stack fails build or lint and cannot be fixed within the
  current scope, revert the dependency migration and report the remaining audit
  blocker.

Rollback path:

- Restore `.eslintrc.json`, previous package versions, and the previous
  `package-lock.json`.
- Remove `eslint.config.mjs`.

## Impact Surface

- Frontend dependency versions and package lock.
- Frontend lint command and ESLint configuration.
- Next.js generated TypeScript configuration.
- Three lint-driven frontend state updates in layout/workspace components.

Runtime API contracts, backend behavior, database schema, sandbox behavior, and
AI provider behavior are not changed.

## Primitive Acceptance Criteria

- `npm audit --audit-level=moderate` reports zero vulnerabilities at the root.
- `npm run typecheck` passes from a clean `.next` state.
- `npm run lint` passes through ESLint CLI.
- `npm run build` passes with Next.js 16.
- Backend build and tests still pass.
- `mcp-code-analyzer` build and audit still pass.
