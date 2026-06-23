# One-Command Startup

## Problem Definition

A fresh checkout should have one local command that restores project
dependencies, creates a real local database, collects only optional AI provider
configuration, and starts the real backend and frontend. Local startup must not
require PostgreSQL, Docker, administrator database setup, or database passwords.

## Option Comparison

- Keep manual PostgreSQL setup: rejected because teammates were blocked by
  local passwords, missing roles, and administrator setup.
- Use Docker PostgreSQL: rejected for normal local startup because Docker
  Desktop/Colima still requires installation, startup, and OS authorization.
- Use in-memory EF storage: rejected because it is not a durable real database
  for delivered local runs.
- Use a repository-owned SQLite file: selected. SQLite is a real local database
  backed by a normal file, requires no administrator service, and is supported
  by EF Core through the official provider.

## Research Basis

- npm `ci` is the official clean-install command for lockfile-based dependency
  restoration:
  https://docs.npmjs.com/cli/v8/commands/npm-ci
- ASP.NET Core configuration reads environment variables, and `__` is the
  cross-platform delimiter for nested configuration keys:
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/
- `SmtpClient.EnableSsl` uses SMTP STARTTLS, so the local SMTP defaults use
  port 587 with TLS enabled; port 465 implicit TLS is not supported by this
  client:
  https://learn.microsoft.com/en-us/dotnet/api/system.net.mail.smtpclient.enablessl
- Next.js supports `.env.local` for local environment variables:
  https://nextjs.org/docs/pages/building-your-application/configuring/environment-variables
- EF Core lists `Microsoft.EntityFrameworkCore.Sqlite` as the SQLite provider:
  https://learn.microsoft.com/en-us/ef/core/providers/
- Microsoft.Data.Sqlite connection strings use `Data Source` for the database
  file path:
  https://learn.microsoft.com/dotnet/standard/data/sqlite/connection-strings
- Npgsql keyword connection strings remain supported for explicit external
  PostgreSQL use:
  https://www.npgsql.org/doc/connection-string-parameters
- Docker documents `DOCKER_HOST`, contexts, Unix sockets, and Windows named
  pipes for Docker Engine clients:
  https://docs.docker.com/reference/cli/docker/
- Docker Desktop for Mac documents the optional `/var/run/docker.sock` symlink
  to `$HOME/.docker/run/docker.sock`; Docker Desktop for Linux documents the
  per-user `$HOME/.docker/desktop/docker.sock` location:
  https://docs.docker.com/desktop/setup/install/mac-permission-requirements/
  https://docs.docker.com/desktop/troubleshoot-and-support/faqs/linuxfaqs/

## State Machine

### Local Startup

- States: doctor check, prerequisite check, local config loading, SQLite config
  write, dependency restore, backend starting, backend healthy, frontend
  starting, running, failed.
- Events: command started, required command missing, Windows npm shim discovered,
  `.env.local` read, stale `LocalLlm__*` values found, repeated AI key paste
  found, SQLite directory missing, SQLite config written, AI key found, AI key
  missing, user enters AI key, user submits blank AI key, restore command fails,
  backend health check passes, backend exits, frontend exits.
- Guards: Node 20+ is required; `npm` and `dotnet` must be available for
  dependency restoration; local startup always supplies
  `Database__Provider=Sqlite`, `ConnectionStrings__DefaultConnection`,
  `SeedAdmin__Email`, and `SeedAdmin__Password`; SMTP settings are optional,
  never prompted for, and may come from environment variables, user secrets,
  or `.env.local`; only `Deepseek__ApiKey` may be requested from the user.
- Transitions: Windows npm discovery prefers an executable shim such as
  `npm.cmd`; stale `LocalLlm__*` values move to local AI cleanup; repeated
  DeepSeek key pastes move to key normalization; missing SQLite directory moves
  to directory creation; generated SQLite config moves to dependency restore;
  missing AI key moves to optional prompt; blank AI key disables local AI;
  backend health success moves to frontend startup and URL output; missing
  system commands, restore failure, or backend health timeout move to failed.
- Side effects: create `.local-data/`; write or update `.env.local`; remove
  stale local `LocalLlm__*` provider settings and write
  `LocalLlm__Enabled=false`; run `npm ci` when root dependencies are absent or
  the ignored lockfile hash marker does not match `package-lock.json`; run
  `dotnet restore`; create the SQLite database through EF Core backend startup;
  append backend logs to gitignored local log files; start backend and frontend
  child processes.
- Failure paths: missing non-interactive AI configuration may disable local AI;
  missing system runtimes fail with install guidance; backend startup failure
  reports the health URL, recent backend error log, and repair guidance for
  local SQLite regeneration or explicitly configured external databases.
- Rollback path: restore `package.json` `dev` to the previous command, remove
  SQLite provider support, remove `scripts/dev-local-database.mjs`, restore the
  previous startup script behavior, and delete this document update.

### AI Local Configuration

- States: key configured, repeated key repair, key invalid, key missing, prompt
  shown, key stored, AI explicitly disabled.
- Events: `Deepseek__ApiKey` found in shell or `.env.local`, same key pasted
  more than once, multiple different key fragments found, key missing, user
  enters key, user submits blank value, `Deepseek__Enabled=false` configured.
- Guards: frontend never receives provider keys; provider calls remain backend
  owned; blank key cannot be treated as a real AI provider.
- Transitions: existing valid key starts normally; repeated same-key paste is
  normalized to one key value; invalid key moves to optional prompt; entered
  valid key is stored in `.env.local` with `Deepseek__Enabled=true`; blank input
  stores `Deepseek__Enabled=false` and the backend returns structured provider
  unavailable errors for AI features.
- Side effects: write AI provider key only to `.env.local`, which remains
  untracked.
- Failure paths: local startup can run with AI disabled; no mock AI content is
  returned.
- Rollback path: remove the prompt and return to manual user-secrets or hosting
  secret configuration only.

## Impact Surface

- Root startup scripts and local dependency restore marker behavior.
- Docker-compatible sandbox runtime discovery and readiness diagnostics.
- Backend EF Core provider selection for SQLite local startup and PostgreSQL
  external deployment.
- Local-only environment file workflow through `.env.local`.
- Gitignored `.local-data/` database storage.
- README, acceptance, TRD, and behavior-level test documentation.
- No change to production API contracts, RBAC, grading contracts, frontend
  provider isolation, or hidden-test protection.

## Primitive Acceptance Criteria

- `npm run dev` uses a cross-platform startup script.
- `npm run dev:doctor` reports local readiness without starting servers,
  restoring dependencies, or writing secrets.
- Local startup and doctor output report whether a Docker-compatible sandbox
  runtime socket was detected for Run and Submit.
- Local startup discovers Docker through `DOCKER_HOST`, Docker CLI context,
  `/var/run/docker.sock`, Docker Desktop user sockets, and Colima's default
  socket when available.
- On a fresh checkout with Node and npm available, startup installs root npm
  dependencies from `package-lock.json` when needed.
- Repeated startups skip root npm installation when the current
  `package-lock.json` hash already matches the ignored marker in `node_modules`.
- When `dotnet` is available, startup restores `Backend/Backend.sln` before
  starting the backend.
- Startup creates `.local-data/` and configures
  `ConnectionStrings__DefaultConnection=Data Source=<repo>/.local-data/ojsharp-dev.sqlite`.
- Startup configures `Database__Provider=Sqlite` for local development.
- Startup does not prompt for database host, port, role, password, Docker
  setup, PostgreSQL setup, or administrator database privileges.
- Missing seed administrator values use `admin@example.com` and `Admin123!` for
  local startup.
- If `Deepseek__ApiKey` is missing and AI is not explicitly disabled, an
  interactive terminal prompts for a DeepSeek key; a blank response disables
  local AI instead of returning mock AI output.
- If `.env.local` contains the same DeepSeek key pasted repeatedly, startup
  rewrites it to a single key value.
- If `.env.local` contains stale `LocalLlm__*` provider settings, startup
  removes those values and disables LocalLlm for the one-command local run.
- SMTP settings supplied through environment variables or .NET user-secrets
  take precedence over `.env.local` and remain available to the backend.
- On Windows, startup resolves npm to an executable shim instead of an
  extensionless command path.
- When frontend startup begins, startup prints `http://localhost:3000`.
- Startup writes secrets only to `.env.local` or process environment, never to
  tracked config files.
- The backend supports SQLite for local startup and PostgreSQL for explicitly
  configured external database use.
- The backend must become healthy before the frontend startup is handed to the
  user.
- Missing system runtimes or failed restores stop startup with explicit
  remediation guidance.
- Permission failures do not trigger unsafe automatic privilege escalation; the
  CLI gives exact authorization steps and waits for the user or OS to complete
  them.
