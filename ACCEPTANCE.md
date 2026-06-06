# Acceptance Criteria

## Active Agent Contract

- The active root `AGENTS.md` contains the owner's 16-point Codex Engineering Contract.
- The previous GitHub `AGENTS.md` is preserved at `docs/archived-github-agents.md`.
- `docs/archived-github-agents.md` is historical context only and is not the active instruction source.
- New or changed agent-governance behavior is documented in `docs/design/codex-engineering-contract.md`.

## Project Documentation Set

- `docs/BRD.md`, `docs/MRD.md`, `docs/PRD.md`, `docs/TRD.md`, `docs/DesignSpec.md`, and `docs/TestCase.md` exist as minimal project documents.
- Project documents are aligned with `SPEC.md`, the English architecture PDF, the API alignment document, and the current four-module architecture.
- Documentation does not claim that unimplemented runtime behavior has been delivered.

## Dependency Security

- Root `npm audit --audit-level=moderate` reports zero vulnerabilities before deployment.
- `mcp-code-analyzer` `npm audit --audit-level=moderate` reports zero vulnerabilities before deployment.
- Dependency migrations that affect tooling are documented in `docs/design/dependency-security-upgrade.md`.

## AI Runtime Truthfulness

- Student AI assistance does not return mock guidance when no real provider returns a usable response.
- AI-generated assessment and question drafts do not fall back to template content labeled as LLM-generated output.
- Browser UI preview does not render sample content when sandbox output is unavailable.
- Real dependency enforcement is documented in `docs/design/real-dependency-enforcement.md`.
- Missing or failing AI providers return a structured API error instead of fabricated assistant content.

## Real Deployment Readiness

- Backend startup requires a configured `ConnectionStrings__DefaultConnection` value and does not use a hardcoded localhost database fallback.
- Backend startup seeds or repairs only the configured seed administrator and does not create demo student or demo assessment content.
- Sandbox-unavailable executions return `internal_error` instead of task-specific static pass/fail results.
- Production frontend requests require `NEXT_PUBLIC_API_BASE_URL`; localhost API fallback is Development-only.
- Login UI does not prefill or display demo credentials.

## Repository Synchronization

- The local `main` branch tracks `origin/main` from `https://github.com/ZAnimenl/AISD-SS26-Group-7.git`.
- The local checkout contains the latest fetched `origin/main` content before local task changes.
- The working tree has no unintentional local modifications.

## Language

- Project-facing source files, documentation files, and tracked file names use English.
- Non-English duplicate documentation is not retained when an English version exists.
- Authoritative documentation references point to the English architecture PDF.
