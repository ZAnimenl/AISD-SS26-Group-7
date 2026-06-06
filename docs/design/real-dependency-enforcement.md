# Real Dependency Enforcement

## Problem Definition

Runtime paths must not use hardcoded demo data, mock AI responses, static execution fallbacks, sample previews, or local defaults as substitutes for real backend, database, sandbox, or AI provider behavior.

## Option Comparison

- Keep fallback behavior and label it more clearly: rejected because deployed features would still appear to work without the real dependency.
- Disable affected features when dependencies are missing: acceptable for dependency failure paths, but not sufficient for provider-backed features.
- Route affected features through real providers and fail explicitly when unavailable: selected. This keeps runtime behavior truthful and preserves attempts without fabricating results.

## Research Basis

- DeepSeek official Chat Completions documentation defines `POST /chat/completions`, `response_format` with `json_object`, and response `usage.prompt_tokens` / `usage.completion_tokens`. The implementation uses non-streaming chat completions so content and token usage are returned together.

## State Machine

### Backend Startup

- States: configuration loading, database initialization, seed admin initialization, ready, failed.
- Events: configuration resolved, database unavailable, seed admin missing, initialization complete.
- Guards: every backend run requires `ConnectionStrings__DefaultConnection`, `SeedAdmin__Email`, and `SeedAdmin__Password`.
- Transitions: missing configuration goes to failed; successful seed admin initialization goes to ready.
- Side effects: create or repair the configured seed administrator only.
- Failure paths: fail startup when database initialization or seed administrator validation fails.
- Rollback path: restore the previous startup seeding behavior and production appsettings default.

### AI Assistance And Draft Generation

- States: request received, provider selected, provider completed, provider unavailable, response recorded, error returned.
- Events: provider configured, provider returns content and usage, provider error, no provider configured, generated JSON invalid.
- Guards: student AI assistance requires active attempt and assessment AI enabled; draft generation requires administrator role and a real provider response.
- Transitions: provider success records real content and usage; provider failure returns `AI_PROVIDER_UNAVAILABLE` or `AI_DRAFT_GENERATION_FAILED`.
- Side effects: student AI interactions are persisted only for real provider responses or platform safety-blocked direct-solution requests.
- Failure paths: return structured errors without mock content.
- Rollback path: restore the previous mock service registration and endpoint dependencies.

### Code Execution

- States: run requested, sandbox executing, sandbox completed, sandbox unavailable, result persisted.
- Events: test selected, sandbox returns result, sandbox process unavailable, timeout.
- Guards: all runs use the sandboxed `ICodeRunner`; task-specific static fallback may not mark tests passed.
- Transitions: sandbox unavailable persists `internal_error`.
- Side effects: execution records and submissions store the real sandbox result or a dependency error.
- Failure paths: public and hidden tests fail closed with `internal_error` when the sandbox is unavailable.
- Rollback path: restore the removed fallback helper in `CodeEvaluationService`.

### Browser Preview

- States: no run, run in progress, sandbox output available, sandbox output missing.
- Events: run started, run completed with HTML output, run completed without HTML output.
- Guards: preview iframe renders only sandbox-produced HTML.
- Transitions: missing preview HTML renders a no-output state.
- Side effects: none outside UI rendering.
- Failure paths: show an empty state instead of sample preview content.
- Rollback path: restore sample preview construction in `TaskVerificationPreview`.

## Impact Surface

- Backend startup configuration and seed administrator initialization.
- Backend AI assistance and LLM draft-generation endpoints.
- Backend execution result handling.
- Frontend login, API base URL selection, AI suggestion handling, and preview rendering.
- Project documentation and acceptance criteria.

## Primitive Acceptance Criteria

- The backend does not use a hardcoded local database connection string when `ConnectionStrings__DefaultConnection` is absent.
- The backend does not seed demo student users or demo assessments automatically in any environment.
- AI assistance never returns mock guidance when no configured provider returns a response.
- LLM draft generation never labels template content as LLM-generated.
- Sandbox-unavailable runs and submissions return dependency errors instead of static pass/fail results.
- Browser preview renders only sandbox-produced HTML and otherwise shows that no real preview output is available.
- Login UI does not prefill demo credentials or display seed/demo credential values.
- Production frontend requests require `NEXT_PUBLIC_API_BASE_URL` instead of falling back to localhost.
