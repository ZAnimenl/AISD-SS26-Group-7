# Test Case Catalogue

This catalogue defines behavior-level tests to keep implementation aligned with
the active requirements. It does not replace automated test files.

## Authentication and Role Access

- Unauthenticated users are blocked from protected student and administrator
  routes.
- Students cannot access administrator-only features.
- Administrators can access assessment management and reports.

## Assessment Management

- Administrators can create an assessment with title, description, duration,
  status, and AI enabled state.
- Administrators can create tasks using the supported task categories.
- AI-generated assessment drafts are provider-backed, editable before
  publication, and never template fallbacks labeled as LLM output.
- AI-generated assessment drafts request enough provider output tokens for
  structured JSON and return an actionable truncation error instead of a raw JSON
  parse failure when the provider cuts off output.
- The administrator create-assessment page does not show shared prototype
  reference or shared prototype version inputs.

## Student Workspace

- Students can start an active assessment and open the browser workspace.
- Starting an assessment shows a pending state while the backend resolves the
  real active attempt and prevents duplicate start clicks.
- Direct deep links to student assessment start, workspace, and review pages
  resolve the `assessment_id` from the URL and do not show not-found states for
  assessments returned by the backend.
- Student assessment start pages list backend-provided public question previews
  for active assessments without hidden tests, administrator notes, grading
  configuration, or other administrator-only data.
- Workspace context displays public task details and starter files.
- Workspace APIs do not require frontend-sent `session_id` or `attempt_id`.
- Autosave persists selected language, active file, file contents, and version.
- Browser preview renders sandbox-produced HTML or a no-output state, never
  sample task content.
- Workspace task navigation, AI assistant, and output panels can be collapsed,
  expanded, and resized without sending extra backend state.
- Output panel headers and bodies use opaque readable surfaces so sandbox logs
  do not visually blend with editor/sidebar text behind them.
- Browser preview tasks whose visible starter file is named
  `todo_summary_panel.py` or `todo_summary_panel.js` still run real public
  checks that import `TodoSummaryPanel`.
- Final submission shows saving/submitting progress and does not navigate to
  review until the backend confirms submission.

## Run and Submit

- Run uses public checks and returns safe stdout, stderr, status, and public test
  feedback.
- With `DOCKER_HOST` pointed at a real Docker-compatible runtime, sandbox
  integration tests execute Python and JavaScript submissions in the grader
  container.
- If the sandbox grader is unavailable, run and submit report `internal_error`
  rather than static task-specific pass/fail results.
- Submit evaluates final work and returns visible and hidden test summary counts
  without hidden inputs or expected outputs.
- Expired or closed attempts reject new runs and submissions.
- Under local SQLite, active-attempt expiry checks for start, workspace, run,
  submit, and AI request flows do not fail because of provider-specific
  `DateTimeOffset` comparison translation.

## AI Assistance

- AI assistance is hidden or blocked when disabled for the assessment.
- AI interactions record message, response, semantic tags, input tokens, output
  tokens, total tokens, assessment, task, and attempt ownership.
- AI requests include active file name, selected language, visible
  selected-language file contents, active file content, and latest public run
  output when available.
- AI provider prompts request structured JSON with student-visible Markdown and
  an optional active-file replacement suggestion.
- AI Apply buttons are shown only for structured suggestions whose target file
  and language match the current workspace state.
- Markdown code blocks in explanation-only AI responses remain readable but are
  not auto-applied as file replacements.
- Direct complete-solution requests receive a safety response rather than a full
  answer.
- Missing or failing AI providers return a structured unavailable error instead
  of mock guidance.
- AI chat may show the student's sent intent immediately, but assistant content
  is displayed only after the backend returns a real provider response.

## Truthful Optimistic UI

- Long-running admin actions show operation-specific pending copy and disable
  duplicate clicks.
- Data-loading pages show backend-loading or backend-error states instead of
  rendering an apparently empty list while requests are still pending.
- Failed mutations preserve local form values and show the backend error.

## Startup Configuration

- A fresh checkout can use `npm run dev` as the local startup command.
- `npm run dev` installs root npm dependencies when missing or stale and runs
  `dotnet restore Backend/Backend.sln` before backend startup.
- Repeated `npm run dev` starts skip root npm installation when the ignored
  local install marker already matches the current `package-lock.json` hash.
- `npm run dev` writes `Database__Provider=Sqlite` and a SQLite
  `ConnectionStrings__DefaultConnection` pointing to
  `.local-data/ojsharp-dev.sqlite` without prompting for database information.
- Repeated `npm run dev` starts reuse `.local-data/ojsharp-dev.sqlite` and do
  not create additional database instances or duplicate npm dependency trees for
  the same lockfile.
- `npm run dev:doctor` reports local prerequisite and configuration readiness
  without starting servers or writing secrets.
- PostgreSQL URLs such as
  `postgresql://postgres:password@localhost:5432/aisd_ss26_group_7` are
  still accepted and normalized for explicit external database use.
- Missing seed administrator values use `admin@example.com` and `Admin123!`;
  `Deepseek__ApiKey` is the only interactive secret prompt unless AI is
  explicitly disabled.
- `npm run dev` runs the backend seed-admin-only path before backend health
  reuse/start so the local administrator login is repaired without manual
  database deletion.
- If an existing local Backend process is already listening on the configured
  backend port, `npm run dev` restarts it when that process can be safely
  identified so the backend serves the current checkout and startup config.
- If `.env.local` contains the same DeepSeek API key pasted repeatedly,
  `npm run dev` rewrites it to one key value before backend startup.
- If `.env.local` contains stale `LocalLlm__*` values, `npm run dev` removes
  those provider settings and writes `LocalLlm__Enabled=false` so local startup
  uses only the supported DeepSeek configuration path.
- On Windows, npm command discovery prefers `npm.cmd` or another executable npm
  shim over the extensionless `npm` shim so frontend startup does not fail with
  `spawn ... npm ENOENT`.
- If an old local Next.js process is already listening on the frontend port,
  `npm run dev` restarts it when the process can be safely identified so the
  printed frontend URL serves the current checkout.
- When frontend startup begins, the CLI prints `http://localhost:3000` as the
  app URL.
- Backend startup failures explain likely repair steps for missing database,
  wrong credentials, insufficient PostgreSQL privileges, Docker permission
  issues, and missing system runtimes.
- Local startup writes entered secrets only to `.env.local`, which remains
  untracked.
- The frontend starts only after the backend health endpoint returns a
  successful response.
- Backend startup fails when `ConnectionStrings__DefaultConnection`,
  `SeedAdmin__Email`, or `SeedAdmin__Password` is missing outside the local
  startup script.
- Backend startup creates or repairs only the configured seed administrator and
  does not create demo users or demo assessments.
- Local development login shows a quick fill action for
  `admin@example.com` / `Admin123!`; production login builds do not expose that
  local demo action.
- Visiting `/login` with valid stored auth redirects to the stored user's
  dashboard without clearing the token.
- Successful login stores the backend token and stays on the matching
  administrator or student dashboard.
- After local default administrator login, `/api/v1/admin/dashboard` returns a
  successful response under SQLite.
- Backend 401 responses clear stored auth before navigation to `/login`.
- Non-auth backend data errors remain on the current page and show the real
  error instead of clearing auth or returning to `/login`.

## Reporting

- Administrator reports include score, status, AI interaction count, total
  tokens, average tokens per interaction, and token efficiency indicator.
- Student-facing result views do not expose hidden tests or administrator notes.

## Security

- Frontend code does not access database, sandbox, or external AI provider
  APIs directly.
- Student code is not executed by frontend JavaScript or normal backend request
  handlers.
- Secrets and provider keys are not committed.
