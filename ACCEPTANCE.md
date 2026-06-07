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
- AI-generated assessment and question drafts request enough provider output tokens for structured JSON and report provider truncation as an actionable draft-generation error instead of exposing raw JSON parser failures.
- Browser UI preview does not render sample content when sandbox output is unavailable.
- Real dependency enforcement is documented in `docs/design/real-dependency-enforcement.md`.
- Missing or failing AI providers return a structured API error instead of fabricated assistant content.

## Real Deployment Readiness

- One-command local startup is documented in `docs/design/one-command-startup.md`.
- `npm run dev` restores root npm dependencies and backend NuGet packages before starting local servers when required tools are available.
- Repeated `npm run dev` starts do not reinstall root npm dependencies when the current `package-lock.json` hash already matches the ignored local install marker.
- `npm run dev` creates or reuses the gitignored SQLite database file `.local-data/ojsharp-dev.sqlite` without asking for database credentials, Docker setup, PostgreSQL setup, or administrator privileges.
- `npm run dev` writes `Database__Provider=Sqlite`, a SQLite `ConnectionStrings__DefaultConnection`, and local seed administrator defaults to `.env.local` when needed.
- `npm run dev` prompts only for missing DeepSeek local configuration, writes prompted secrets only to `.env.local`, and starts the frontend only after backend health succeeds.
- `npm run dev` runs the backend seed step before backend startup, restarts an existing local Backend process on the configured port when it can be safely identified, and verifies that reused external backends accept the configured seed administrator.
- `npm run dev` normalizes accidental repeated DeepSeek API key pastes and disables stale `LocalLlm__*` local overrides so local AI setup does not require provider-level troubleshooting.
- `npm run dev` resolves Windows npm shims to an executable command and prints the frontend URL when the frontend startup step begins.
- `npm run dev` restarts an old local Next.js process on the frontend port when it can be safely identified so the printed URL serves the pulled code.
- `npm run dev:doctor` reports local prerequisite and configuration readiness without starting servers or writing secrets.
- Backend startup supports SQLite for local development and PostgreSQL for explicit external database deployment.
- Backend startup failures produce CLI repair guidance for local SQLite regeneration, external database configuration, Docker sandbox permission, and missing-runtime failures.
- Backend startup seeds or repairs only the configured seed administrator and does not create demo student or demo assessment content.
- Local development login exposes a quick fill action for the seeded administrator account and successful sign-in remains on the role dashboard instead of returning to `/login`.
- After local default administrator sign-in, the administrator dashboard API succeeds under the repository-owned SQLite database.
- Local SQLite-backed active-attempt checks for start, workspace, run, submit, and AI request flows do not fail on `DateTimeOffset` ordering or expiry comparisons.
- Authentication state is cleared on backend 401 responses or explicit logout, not merely because the login page mounted or a non-auth data request failed.
- Sandbox-unavailable executions return `internal_error` instead of task-specific static pass/fail results.
- Real sandbox verification passes against a Docker-compatible runtime when `DOCKER_HOST` points to the configured runtime socket.
- Production frontend requests require `NEXT_PUBLIC_API_BASE_URL`; localhost API fallback is Development-only.
- Production login UI does not prefill or display demo credentials.

## Truthful Optimistic UI

- Truthful latency handling is documented in `docs/design/truthful-optimistic-ui.md`.
- Workspace IDE panel behavior is documented in `docs/design/workspace-ide-panels.md`.
- LLM draft generation, AI assistance, run, start-attempt, final submission, and admin mutation controls show real pending states while backend/provider work is in progress.
- The UI does not mark generated, saved, submitted, or passed states until the backend confirms the real result.
- Loading pages show backend-loading or backend-error states instead of appearing empty while data is still pending.
- Failed long-running actions preserve user-entered data and show the real backend/provider error.
- Dynamic assessment routes resolve the URL `assessment_id` on production Next.js builds and do not treat existing backend assessments as missing.
- The administrator create-assessment page does not expose shared prototype reference or shared prototype version inputs.
- Student assessment start pages show backend-provided public question previews for active assessments without exposing hidden tests, administrator notes, or grading configuration.
- Workspace task, AI, and output panels can be collapsed/expanded and resized without changing backend state.
- Sandbox output surfaces use opaque readable backgrounds and do not visually merge with editor or sidebar text.
- Browser-preview runs for the platform Todo summary task resolve the visible starter file even when legacy tests import `TodoSummaryPanel`.
- AI workspace assistance is documented in `docs/design/ai-agent-workspace-context.md`.
- AI assist requests include active file name, visible selected-language files, and latest public run feedback when available.
- AI Apply actions appear only for backend-validated structured suggestions targeting the active file and selected language.
- Arbitrary Markdown code blocks in AI responses are not treated as file replacements.
- AI structured suggestions preserve required public function names or exports from visible starter files before the frontend can apply them.

## Repository Synchronization

- The local `main` branch tracks `origin/main` from `https://github.com/ZAnimenl/AISD-SS26-Group-7.git`.
- The local checkout contains the latest fetched `origin/main` content before local task changes.
- The working tree has no unintentional local modifications.

## Language

- Project-facing source files, documentation files, and tracked file names use English.
- Non-English duplicate documentation is not retained when an English version exists.
- Authoritative documentation references point to the English architecture PDF.
