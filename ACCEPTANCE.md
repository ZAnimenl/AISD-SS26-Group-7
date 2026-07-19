# Acceptance Criteria

## Assessment Authoring and Review UX

- Admin assessment creation has three visible stages: Assessment basics,
  Generate & review, and Delivery settings.
- Generated questions and tests can be reviewed and edited before timing and
  availability are finalized.
- Delivery settings cannot be finalized with zero questions or a non-positive
  duration.
- The ongoing student workspace shows one question at a time with Previous,
  Next, and compact direct navigation.
- The pre-start page explicitly labels the assessment expiration deadline and
  explains that access becomes review-only afterward.
- The code editor is the largest workspace area by default; the output starts
  compact and the narrower AI panel has a visible horizontal resize handle.
- On narrow screens, auxiliary workspace panels start collapsed so the editor
  remains usable without horizontal page overflow.
- Question navigation preserves autosaved per-question workspace state.
- AI-enabled student and admin reports show a concise AI-use and
  demonstrated-understanding summary below the score overview.
- Student review shows the automated assessment insight directly above the
  submitted reflection.
- Report summaries use stored grading evidence and distinguish pending, failed,
  and completed analysis.
- Dashboard and report percentage scores include compact accessible charts that
  exactly match their displayed numeric values.
- Dashboard score cards do not show a redundant statistic icon above a donut.
- The dashboard assessment preview fits its panel without a horizontal
  scrollbar.
- Assessment duration can be entered directly as a number or adjusted with the
  slider and step buttons.
- Future assessment availability is presented as neutral scheduling
  information; warning styling is reserved for actually unavailable attempts.
- Expired pre-start pages use explicit review-only copy instead of language that
  implies work can still continue.
- Review and reflection deep links without a submission explain the real state
  and route an active attempt back to its workspace.
- Logout clears local authentication and leaves protected pages immediately,
  even when backend token revocation is slow or unavailable.
- Authentication is scoped to a browser window: administrator and student
  accounts can remain open concurrently in separate same-origin windows, and
  logout or HTTP 401 in one window does not clear authentication in another.
- Window authentication survives navigation and reload within that window but
  is not persisted after the window is closed.
- Completed AI scoring displays all four rubric subsections with their score
  and a concise evidence-based summary.

## Active Agent Contract

- The active root `AGENTS.md` contains the owner's 16-point Codex Engineering Contract.
- The previous GitHub `AGENTS.md` is preserved at `docs/archived-github-agents.md`.
- `docs/archived-github-agents.md` is historical context only and is not the active instruction source.
- New or changed agent-governance behavior is documented in `docs/design/codex-engineering-contract.md`.

## Project Documentation Set

- `docs/BRD.md`, `docs/MRD.md`, `docs/PRD.md`, `docs/TRD.md`, `docs/DesignSpec.md`, and `docs/TestCase.md` exist as minimal project documents.
- Project documents are aligned with `SPEC.md`, the English architecture PDF, the API alignment document, and the current four-module architecture.
- Documentation does not claim that unimplemented runtime behavior has been delivered.
- Automatic AI usage scoring and reflection behavior is documented in
  `docs/design/automatic-ai-usage-scoring.md`.

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
- Automatic AI grading failures preserve the functional submission and expose
  a pending or failed state instead of assigning a fabricated score or zero.

## Automatic AI Usage Scoring

- AI-disabled assessments produce only a `0-100` Functional Score and do not
  require AI interaction or reflection.
- AI-enabled assessments require at least one successfully logged platform AI
  interaction before final submission.
- AI-enabled attempts produce a `0-100` Functional Score, a separate `0-100` AI
  Usage Score, and a Final Score equal to their arithmetic mean.
- AI Usage Score weights are Prompt quality and context 30%, Token and
  interaction efficiency 40%, Critical evaluation and adaptation 20%, and
  Reflection quality and consistency 10%.
- Token and interaction efficiency contains a semantic behavioral assessment,
  a 0-15 deterministic reference-efficiency component for measured fully
  passing tasks, and a 10-point objective repetition metric. A missing
  reference baseline preserves the legacy 30-point semantic assessment.
- No fixed absolute token threshold, including 2,500 tokens, and no
  cohort-relative token usage contributes to the AI Usage Score.
- Automatic grading stores rubric version, model identifier, criterion scores,
  evidence, confidence, and summary.
- Deterministic repetition and rapid-accept measurements cannot be overwritten
  by the grading LLM.
- Actionable suggestion events record response visibility, decision type,
  elapsed decision time, unchanged application, editing, rejection, dismissal,
  and undo where applicable.
- Each actionable suggestion applied unchanged within three seconds deducts one
  Critical evaluation point, up to eight points, except where an immediate undo
  or substantial edit cancels the deduction.

## Timed Submission Reflection

- AI-enabled code submission freezes the workspace before reflection begins.
- The reflection uses the approved consolidated prompt and accepts no more than
  100 words.
- The backend owns a ten-minute deadline that survives refresh and reconnect.
- Reflection drafts autosave and the latest draft is finalized automatically at
  timeout.
- An empty reflection receives zero Reflection quality points but preserves the
  Functional Score.
- AI-disabled assessments bypass reflection entirely.
- Confirmed submission immediately freezes the visible timer and disables editor, task, file, language, Run, AI, suggestion, and duplicate-submission controls.
- Code remains frozen while the AI-enabled reflection is pending.
- Hidden-test evaluation time does not consume the backend-owned ten-minute reflection window.

## Canonical Assessment Prototype and Preview

- `assessmentPrototype/` contains the source-only canonical Todo UI, FastAPI API, Peewee ORM, and SQLite configuration.
- Generated tasks extend the canonical Todo source/contracts and do not invent a replacement base application.
- Students install no prototype dependencies locally.
- Each assessment task restores its own embedded AI conversation and supports a
  JSON transcript download containing that task's persisted AI inputs and outputs.
- The workspace AI usage card is separated by task and does not show another
  task's tokens or interactions as the active task's usage.
- Each generated task has an administrator-only AI-usage benchmark that considers
  reference token efficiency and the task goal, code context, observed behavior,
  and constraint information supplied to the AI agent.
- Each generated task runs a provider-measured complete-versus-compact public-context baseline after generation. Its stored compression rate, compression ratio, structural-retention score, and availability state remain administrator-only.
- Each completed baseline stores two to five administrator-only minimal-input standard steps with public verification actions; it contains no hidden test or grading implementation.
- The active AI panel shows only that task's observable prompt CpT, prompt TpC, response CpT, response TpC, and context-signal coverage; these inform reference-relative AI Usage scoring only after the task receives a full automated-submission score. It does not expose hidden tests, baseline prompts, or grading implementation.
- Browser preview uses a sandbox-produced document with local CSS/JavaScript inlined.
- Browser preview packaging uses the trusted lightweight sandbox profile; a completed run without a preview shows its timeout, runtime, infrastructure, or invalid-output failure instead of an indefinite unavailable placeholder.
- The preview iframe supports interactive JavaScript, forms, SVG/canvas, dependency diagrams, and locally bundled charts while blocking network and parent access.
- Non-UI previews show task-specific verification; Console shows public checks, stdout, stderr, errors, and metrics.
- Failed tests, runtime errors, and sandbox infrastructure failures are visibly distinct.

## Shared Dropdown

- No native `select` remains on administrator or student pages.
- The reusable dropdown supports keyboard navigation, outside-click closing, disabled behavior, and form serialization.
- Dropdown option lists are not clipped by cards, panels, or page scrolling and remain within the visible viewport.

## Assessment Expiration

- Administrators must set an assessment expiration date and time when creating or editing an assessment.
- Expiration must be later than the effective start time.
- New attempt expiry is capped at the earlier of the configured attempt duration and assessment expiration.
- After assessment expiration, students can review submitted results but cannot start, continue, run, save, use AI, or submit code.
- Assessment duration must be a positive whole number and is validated by both administrator UI and backend API.
- Administrator duration is selected with a 1–240 minute range slider and accessible five-minute adjustment buttons.

## Reporting Aggregation

- Dashboard Average is calculated from one Final Score per submitted attempt with completed AI grading.
- Registered users, active attempts, failed/pending AI grades, and raw per-question rows do not affect Dashboard Average.

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
- `npm run dev:doctor` reports sandbox runtime readiness for Run and Submit without making Docker a startup requirement.
- Docker startup diagnostics distinguish a configured endpoint from a
  reachable runtime.
- Docker Desktop named-pipe diagnostics use the CLI-compatible endpoint form
  and report a running engine as detected.
- Generated assessment workspaces copy canonical Todo frontend, backend, and
  database starter files from the packaged `assessmentPrototype` source.
- LLM output may add task-specific files and tests but cannot replace canonical
  base files with a newly invented application.
- Generated REST API and bug-fix tasks support the administrator-required
  JavaScript language with canonical Node/Express starter modules and executable
  JavaScript test code, without requiring student dependency installation.
- New REST API and bug-fix drafts select Python and JavaScript by default, with
  seven canonical backend modules for each; TypeScript is not offered as a new
  admin or student language option.
- Browser-task checks can use platform-provided global `JSDOM`, IndexedDB, and
  fetch mocks inside the isolated grader without dependency installation or
  `JSDOM is not defined` failures.
- Multi-question AI drafts use bounded parallel provider work with deterministic
  task ordering and no partial persistence when one question fails.
- Backend startup supports SQLite for local development and PostgreSQL for explicit external database deployment.
- Backend startup failures produce CLI repair guidance for local SQLite regeneration, external database configuration, Docker sandbox permission, and missing-runtime failures.
- Backend startup seeds or repairs only the configured seed administrator and does not create demo student or demo assessment content.
- Local development login exposes a quick fill action for the seeded administrator account and successful sign-in remains on the role dashboard instead of returning to `/login`.
- After local default administrator sign-in, the administrator dashboard API succeeds under the repository-owned SQLite database.
- Local SQLite-backed active-attempt checks for start, workspace, run, submit, and AI request flows do not fail on `DateTimeOffset` ordering or expiry comparisons.
- Authentication state is cleared on backend 401 responses or explicit logout, not merely because the login page mounted or a non-auth data request failed.
- Registration sends a six-digit verification code by email and exposes it beside the submitted email only as a fallback when SMTP delivery is unavailable; the code can complete the existing verification flow.
- Abandoning an uncompleted verification challenge does not reserve its requested username. Persisted users still own their usernames, and when pending registrations share a username only the first successful completion creates an account, including under concurrent completion requests and case-insensitive matches.
- Backend config reports `real_sandbox_enabled=false` when no Docker-compatible runtime is reachable, and the student workspace disables Run and Submit in that state.
- Sandbox-unavailable executions return `internal_error` instead of task-specific static pass/fail results.
- Real sandbox verification passes against a Docker-compatible runtime when `DOCKER_HOST` points to the configured runtime socket.
- Production frontend requests require `NEXT_PUBLIC_API_BASE_URL`; localhost API fallback is Development-only.
- Production login UI does not prefill or display demo credentials.
- Production-facing UI copy does not expose backend, provider, sandbox, or
  persistence implementation details in routine loading and save states.
- Student review uses horizontal score bars for Functional and AI Usage,
  reserves the enlarged donut for Final Score, and places the student's
  reflection directly below the final score.
- Donut-based metric cards use consistent visual slots and vertical alignment
  across dashboard and report grids.
- Administrator dashboards group operational counts separately and display
  Average Final as a distinct featured score card.
- Student dashboards group assessment-status counts separately and display
  Average Score as a distinct featured score card.
- Student dashboard and assessment-list pages separate assessments that are
  currently available to start or continue from scheduled, expired, and other
  unavailable assessments.
- Administrator report list, aggregate, and per-student views use supporting
  bars for Functional and AI Usage while reserving the prominent donut
  treatment for Final Score.
- Administrator report overview cards place the featured Final Score directly
  below the assessment title, before all supporting metrics.
- Administrator report detail places the featured Final Score at the left of
  the aggregate score row.

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
- Student assessment start pages verify the real backend workspace is readable after start-attempt succeeds before navigating to the IDE.
- Student workspace language controls expose only languages allowed by the active question, and autosave, run, submit, and AI requests do not send disallowed selected-language or file-language values.
- Workspace task, AI, and output panels can be collapsed/expanded and resized without changing backend state.
- Sandbox output surfaces use opaque readable backgrounds and do not visually merge with editor or sidebar text.
- Browser-preview runs for the platform Todo summary task resolve the visible starter file even when legacy tests import `TodoSummaryPanel`.
- Warm, non-timeout public runs for normal-sized tasks, including the canonical
  browser preview and public checks, complete within ten seconds on supported
  local development hardware; cold grader warmup returns a quick retryable
  unavailable result instead of holding the student request for minutes.
- Generated public checks have a bounded command deadline below the host guard, so hanging code returns a terminal result rather than leaving Run pending.
- Each sandbox check runs in an ephemeral container that can access only its
  own generated workspace and is removed after success, failure, or timeout.
- AI workspace assistance is documented in `docs/design/ai-agent-workspace-context.md`.
- AI assist requests include active file name, visible selected-language files, and latest public run feedback when available.
- AI assist requests use the active question's allowed language after workspace state normalization, not stale or disallowed frontend state.
- AI Apply actions appear only for backend-validated structured suggestions targeting the active file and selected language.
- Arbitrary Markdown code blocks in AI responses are not treated as file replacements.
- AI structured suggestions preserve required public function names or exports from visible starter files before the frontend can apply them.

## Repository Synchronization

- The local `main` branch tracks `origin/main` from `https://github.com/ZAnimenl/AISD-SS26-Group-7.git`.
- The local checkout contains the latest fetched `origin/main` content before local task changes.
- The working tree has no unintentional local modifications.

## Report Video Playback

- Each final-report video poster exposes a standard public HTTPS player link
  for browser PDF viewers and selects the matching MP4.
- The generated report preserves one matching Acrobat RichMedia annotation for
  each browser video link.
- The offline report package contains the PDF, a native HTML5 video gallery,
  all three H.264/AAC MP4 files, and their poster images.
- The offline gallery uses only packaged relative paths and does not require an
  internet connection after extraction.

## Language

- Project-facing source files, documentation files, and tracked file names use English.
- Non-English duplicate documentation is not retained when an English version exists.
- Authoritative documentation references point to the English architecture PDF.
