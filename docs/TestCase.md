# Test Case Catalogue

This catalogue defines behavior-level tests to keep implementation aligned with
the active requirements. It does not replace automated test files.

## Authentication and Role Access

- Unauthenticated users are blocked from protected student and administrator
  routes.
- Students cannot access administrator-only features.
- Administrators can access assessment management and reports.
- Starting or resending registration returns a six-digit code that the
  registration page displays beside the submitted email, whether or not SMTP
  delivery succeeds.
- SMTP settings supplied through environment variables override local settings
  during one-command startup, and TLS is enabled by default for STARTTLS
  providers such as Gmail on port 587.

## Assessment Management

- Administrators can create an assessment with title, description, duration,
  status, and AI enabled state.
- Assessment creation visibly separates basics, generated-question review, and
  delivery settings into three stages.
- Task-count increment and decrement controls have category-specific accessible
  names.
- Title and description are required before generation review, and at least one
  reviewed question is required before delivery settings can be finalized.
- Administrators can create tasks using the supported task categories.
- AI-generated assessment drafts are provider-backed, editable before
  publication, and never template fallbacks labeled as LLM output.
- AI-generated assessment drafts request enough provider output tokens for
  structured JSON and return an actionable truncation error instead of a raw JSON
  parse failure when the provider cuts off output.
- JavaScript-only REST API and bug-fix generation preserves the requested
  language, merges canonical JavaScript backend modules before starter-file
  validation, and requires JavaScript code in every public and hidden test.
- REST API and bug-fix blueprint generation defaults to Python and JavaScript,
  and both languages receive seven canonical backend modules.
- Admin and student language option lists omit TypeScript while legacy backend
  grading/parsing code remains compatible with existing TypeScript records.
- The grader setup exposes global `JSDOM`, preloads IndexedDB, normalizes a full
  `fake-indexeddb` module assignment, and includes `jest-fetch-mock` in the
  versioned sandbox image.
- Multi-question generation observes more than one and no more than two
  concurrent draft pipelines, preserves requested task order and score totals,
  and returns no partial graph after a sibling failure.
- The administrator create-assessment page does not show shared prototype
  reference or shared prototype version inputs.

## Student Workspace

- Students can start an active assessment and open the browser workspace.
- Before starting, students see an explicit assessment expiration timestamp and
  a notice that the workspace becomes review-only afterward.
- The initial workspace gives the code editor more space than the output panel,
  and the AI panel can be resized horizontally with its visible divider.
- At mobile width, the task, AI, and output panels start collapsed so the editor
  remains visible and the page has no horizontal overflow.
- Resizing the editor or AI rail does not overlap file tabs, language selection,
  Run, AI status, usage metrics, or panel controls.
- The task rail displays one active question with Previous, Next, and compact
  direct question navigation.
- Switching questions preserves the selected language, active file, and code
  for every question.
- Starting an assessment shows a pending state while the backend resolves the
  real active attempt and prevents duplicate start clicks.
- Revisiting the pre-start route during an active attempt offers Continue
  attempt instead of implying that a new attempt will be created.

## AI Report Summary

- AI-enabled admin and student reports display the stored grading narrative and
  reflection-understanding assessment below the score overview.
- The student review page renders the automated assessment insight immediately
  above the student's submitted reflection.
- Pending, reflection-pending, failed, and completed grading states display
  distinct truthful copy.
- Student result serialization includes AI grading summary, confidence, and
  details such as `reflection_consistency`.
- Dashboard average-score cards and report percentage cards render a compact
  accessible donut whose value matches the displayed percentage.
- Student review Functional and AI Usage cards show the score donuts without
  redundant statistic icons; the Final Score label appears below its donut.
- Dashboard average-score cards omit the redundant statistic icon when the
  donut is present.
- The dashboard assessment preview has no horizontal scrollbar at desktop
  widths and keeps its visible columns inside the panel.
- Duration changes accept direct numeric entry as well as slider and step-button
  input, clamped to the supported 1-240 minute range.
- A future assessment deadline uses neutral availability styling and copy;
  expired or otherwise unavailable attempts still explain why they cannot open.
- An expired pre-start page does not say that the student can continue working.
- Expired attempts without a submission do not claim that a submitted result is
  available for review.
- A review deep link for an active, unsubmitted attempt offers Continue
  assessment and does not claim that a submission was received.
- A reflection deep link before submission shows an unavailable state and does
  not claim that code is frozen or a draft is autosaving.
- Completed student and administrator AI reports show the four rubric
  subsections, each with its score and a concise stored-evidence summary.
- After start succeeds, the start page verifies the real backend workspace is
  readable before navigating to the IDE.
- Direct deep links to student assessment start, workspace, and review pages
  resolve the `assessment_id` from the URL and do not show not-found states for
  assessments returned by the backend.
- Student assessment start pages list backend-provided public question previews
  for active assessments without hidden tests, administrator notes, grading
  configuration, or other administrator-only data.
- Workspace context displays public task details and starter files.
- Workspace APIs do not require frontend-sent `session_id` or `attempt_id`.
- Workspace language controls show only languages allowed by the active
  question.
- Existing workspace state with disallowed selected-language or file-language
  values is normalized before autosave, run, submit, or AI requests.
- Autosave persists selected language, active file, file contents, and version.
- Browser preview renders sandbox-produced HTML or a terminal status-specific
  timeout/runtime/infrastructure/invalid-output state, never sample task content.
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
- Local startup detects common Docker Desktop and Colima socket locations and
  reports sandbox runtime readiness in doctor/startup output.
- A configured but unreachable Docker host is reported as unreachable, not
  detected or ready.
- Student workspace Run and Submit controls are disabled when backend config
  reports the real sandbox runtime is unavailable.
- A sandbox-disabled Submit control is visibly disabled and labeled
  unavailable instead of looking actionable.
- If the sandbox grader is unavailable, run and submit report `internal_error`
  rather than static task-specific pass/fail results.
- Warm canonical browser-preview runs complete in under ten seconds on
  supported local development hardware, while an unfinished cold image warmup
  returns a retryable unavailable result within the readiness wait bound.
- The synthetic HTML preview selects the trusted lightweight packager only through administrator metadata, inlines local CSS/JavaScript, rejects nested submitted paths, and completes within its five-second warm host bound.
- Concurrent sandbox checks use separate ephemeral containers, cannot list or
  read sibling check workspaces, and leave no run container after completion or
  timeout.
- Submit evaluates final work and returns visible and hidden test summary counts
  without hidden inputs or expected outputs.
- Expired or closed attempts reject new runs and submissions.
- Under local SQLite, active-attempt expiry checks for start, workspace, run,
  submit, and AI request flows do not fail because of provider-specific
  `DateTimeOffset` comparison translation.

## AI Assistance

- AI assistance is hidden or blocked when disabled for the assessment.
- AI-enabled final submission is blocked until at least one AI interaction has
  been successfully persisted for the attempt.
- AI interactions record message, response, semantic tags, input tokens, output
  tokens, total tokens, assessment, task, and attempt ownership.
- Switching tasks restores only that task's persisted AI prompt/response history;
  downloading its transcript returns those same records in JSON form.
- The workspace AI usage card shows only the active task's token and interaction
  totals, including newly completed requests without requiring a page refresh.
- Generated tasks contain a versioned, deterministic AI-usage benchmark with a
  reference token budget, recommended interaction count, and required context
  signals. AI-usage grading receives actual per-task token/context evidence.
- Generated tasks run matched full and compact public-context provider prompts;
  a completed baseline stores the resulting token compression ratio and a
  structural-retention reference score, while unavailable provider runs are
  explicitly marked unavailable rather than estimated.
- A completed baseline stores two to five administrator-only standard steps,
  each with minimal AI input and a public verification action. No standard step
  exposes a hidden test or grading rule.
- The active AI panel shows only that task's prompt/response CpT, TpC, and
  context-signal coverage. For a fully passing submission, deterministic
  reference-relative density, context, and token-cost evidence contributes up
  to 15 behavioral-efficiency points; unavailable baselines retain the legacy
  semantic score. It does not show the administrator-only reference baseline
  or represent a proxy as the research underthinking metric.
- AI requests include active file name, selected language, visible
  selected-language file contents, active file content, and latest public run
  output when available.
- AI requests use the active question's allowed selected language and do not
  send stale disallowed workspace file languages.
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
- Actionable AI responses record when the complete response becomes visible and
  when the student applies, edits before applying, rejects, dismisses, or undoes
  the suggestion.
- Applying an actionable suggestion unchanged within three seconds records a
  `rapid_unchanged_accept` event.
- Explanatory or trivial responses do not produce rapid-accept deductions, and
  immediate undo or substantial editing cancels the deduction.
- Near-duplicate prompt detection considers intervening code changes, suggestion
  actions, and runs rather than exact prompt text alone.

## AI-Enabled Reflection

- After an AI-enabled code submission is frozen, the student sees the single
  required reflection prompt and a backend-owned ten-minute countdown.
- The reflection input enforces a maximum of 100 words and displays a live word
  count.
- Reflection drafts autosave and survive refresh or reconnect without resetting
  the deadline.
- The student can submit the reflection before the deadline.
- At timeout, the latest saved draft is submitted automatically.
- An empty timeout reflection receives zero reflection points but preserves the
  Functional Score and continues automatic AI grading.
- Code editing, Run, AI assistance, and resubmission are disabled while
  reflection is pending.
- AI-disabled assessments do not show or require a reflection.

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
- `npm run dev:doctor` reports whether a Docker-compatible sandbox runtime was
  detected for Run and Submit.
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
- Logout from dashboards and the workspace clears local auth immediately and
  navigates to `/login` without waiting indefinitely for the backend.
- Separate browser windows can hold different administrator and student
  accounts at the same time; logout or an authentication failure in one window
  leaves the other window authenticated.
- Reloading an authenticated window preserves its account, while closing the
  window ends that window-scoped login.
- Legacy shared auth is removed from shared browser storage and requires one
  fresh window-specific login.
- The compact workspace Logout control remains visible and clickable above
  local development overlays.
- Non-auth backend data errors remain on the current page and show the real
  error instead of clearing auth or returning to `/login`.

## Reporting

- AI-disabled reports show only the `0-100` Functional Score.
- AI-enabled reports show separate `0-100` Functional and AI Usage scores and a
  Final Score equal to their arithmetic mean.
- The AI Usage Score contains Prompt quality and context `0-30`, Token and
  interaction efficiency `0-40`, Critical evaluation and adaptation `0-20`,
  and Reflection quality and consistency `0-10`.
- Token and interaction efficiency contains a semantic behavioral assessment,
  deterministic reference-efficiency `0-15` for measured fully passing tasks,
  and objective repetition metrics `0-10`; without a measured task, the
  semantic behavioral assessment retains its legacy `0-30` range.
- Reports do not use or display a fixed 2,500-token efficiency threshold and do
  not use cohort-relative token grading.
- Automatic grading returns criterion-level evidence, rubric version, model,
  confidence, summary, and reflection consistency.
- Provider, timeout, and malformed-output failures display pending or failed AI
  grading without converting the AI Usage Score to zero.
- Administrator reports retain AI interaction count, input/output/total tokens,
  average tokens per interaction, per-task totals, reflection, and suggestion
  event evidence.
- Student-facing result views do not expose hidden tests or administrator notes.

## Security

- Frontend code does not access database, sandbox, or external AI provider
  APIs directly.
- Student code is not executed by frontend JavaScript or normal backend request
  handlers.
- Production secrets and provider keys are not committed. The current private
  course checkout contains dev-only Google OAuth and SMTP values in
  `Backend/Backend/appsettings.Development.json`; those values must be
  rotated/removed before public release.
# Added verification cases (2026-06-19)

- Canonical prototype contains only source/config/test assets and excludes dependencies, caches, logs, databases, and build output.
- Canonical browser UI contains no remote dependencies and references local CSS/JavaScript.
- Browser preview tests inline local CSS and JavaScript in the sandbox artifact.
- The preview iframe permits interactive JavaScript/forms while CSP blocks network and parent access.
- Incomplete starter code returns failed public checks without being relabeled as a runtime or infrastructure failure.
- Submission freezes timer and all mutation controls before workspace save/final submission calls complete.
- AI-enabled confirmed submission routes to reflection; AI-disabled confirmed submission routes to review.
- The ten-minute reflection deadline starts from backend submission confirmation, not from the beginning of hidden-test evaluation.
- AI grading evidence accepts array, object, string, missing, null, and unsupported primitive shapes without throwing.
- Problem statement copy detection tolerates whitespace/punctuation formatting differences and caps only Prompt quality and context at 15/30 with evidence.
- Dashboard final average groups question submissions by attempt and includes only submitted attempts with completed AI grading.
- Every administrator/student dropdown supports arrows, Home/End, Enter/Space, Escape/Tab, outside click, disabled state, and form serialization.

# Added verification cases (2026-06-20)

- Assessment create and edit requests require an expiration timestamp later than the effective start timestamp.
- Student assessment listings expose the configured assessment expiration.
- Starting an attempt after assessment expiration returns `ASSESSMENT_EXPIRED`.
- A late-started attempt expires at the assessment deadline when that deadline occurs before the configured duration.
- Past-deadline assessments expose review navigation for submitted work and no start, continue, or repeat-attempt action, while their assessment status is `closed`.
- Student dashboard and assessment-list pages place active assessments whose
  schedule is currently open in the Active assessments section; not-yet-open,
  past-deadline, closed, archived, and otherwise unavailable assessments appear in
  Other assessments.
- Shared dropdown option lists render at viewport level and open upward when the lower viewport space would clip options.
- Draft generation retries advanced-concern validation with exact task-type vocabulary and supports up to five attempts before failing.
- Assessment create/edit rejects zero, negative, and non-integer duration values before persistence.
- Draft generation validates the requested task category before language-test coverage and retries missing test-code entries with explicit required-language guidance.
- Duration sliders serialize a positive 1–240 minute value on create and edit.
- A frontend task explicitly requiring optimistic UI and conflict resolution satisfies the frontend advanced-concern threshold.

# Added verification cases (2026-06-22)

- Generated HTML starter workspaces contain the exact canonical Todo
  `index.html`, `styles.css`, and `app.js` contents.
- Generated Python starter workspaces contain the packaged canonical FastAPI,
  Peewee, controller, service, repository, schema, and environment files.
- Generated SQL starter workspaces contain the packaged canonical Todo schema,
  seed data, and extension file.
- Task-specific files returned by the LLM remain available alongside canonical
  files, while conflicting canonical file names are overwritten from source.
- Canonical files are included in editable starter-file metadata and use the
  `default-todo-list` prototype reference.
- Docker-backed execution tests run without skip when Docker Desktop is
  available and cover successful Python/JavaScript execution, DOM support,
  timeouts, network isolation, aliases, and syntax errors.
- Routine loading, saving, AI, submission, and execution-unavailable messages
  avoid backend/provider/sandbox implementation wording.
- Student review renders Functional and AI Usage as horizontal score bars,
  renders Final Score as a larger donut, and places Your reflection before
  Submission summary.
- Dashboard and report grids keep donut centers, labels, and card heights
  aligned with adjacent icon and numeric metric cards.
- Administrator dashboard keeps Assessments, Students, Submissions, and AI
  Events as equal operational cards and renders Average Final separately.
- Student dashboard keeps Available, In Progress, and Completed as equal
  operational cards and renders Average Score separately.
- Administrator report list, aggregate summary, and student result rows show
  Functional and AI Usage as score bars and Final as the only featured donut.
- Report overview cards render Final Score above Functional, AI Usage,
  completion, and interaction metrics.
- Report detail renders Final Score as the leftmost aggregate score card.
