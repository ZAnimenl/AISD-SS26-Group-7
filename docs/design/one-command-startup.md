# One-Command Startup

## Problem Definition

A fresh checkout should have one local command that restores project dependencies,
collects required local configuration, and starts the real backend and frontend.
The command must not commit secrets, invent mock provider behavior, or silently
skip runtime dependencies that are required for real assessment flows.

## Option Comparison

- Keep `scripts/dev.ps1` as the only startup path: rejected because it is
  PowerShell-specific and does not handle dependency restore or missing local
  configuration.
- Use Docker Compose for every dependency: rejected for this step because it
  would expand the task into containerizing the backend/frontend deployment
  path.
- Use a project-owned Docker PostgreSQL container only for local development:
  selected for missing or broken local PostgreSQL configuration. It avoids
  asking teammates for host PostgreSQL passwords, uses real PostgreSQL instead
  of a mock database, and can be reset without touching existing host
  PostgreSQL installations or remote databases.
- Use a cross-platform Node startup coordinator: selected. The repo already
  requires Node for Next.js, and a Node script can restore npm packages, run
  `dotnet restore`, prepare a local Docker PostgreSQL database, prompt only for
  AI secrets when database auto-provisioning is available, load `.env.local` for
  the backend, and start both processes without adding Node dependencies.

## Research Basis

- npm `ci` is the official clean-install command for lockfile-based dependency
  restoration in automated or reproducible environments:
  https://docs.npmjs.com/cli/v8/commands/npm-ci
- ASP.NET Core configuration reads environment variables, and `__` is the
  cross-platform delimiter for nested configuration keys:
  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/
- Next.js supports `.env.local` for local environment variables:
  https://nextjs.org/docs/pages/building-your-application/configuring/environment-variables
- Npgsql documents keyword-based connection strings and standard PostgreSQL
  environment variables:
  https://www.npgsql.org/doc/connection-string-parameters
- .NET user-secrets provide local development secret storage outside tracked
  project files:
  https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets
- Docker port publishing can bind a container port to a host address and port;
  binding to `127.0.0.1` keeps the local PostgreSQL port on the loopback
  interface:
  https://docs.docker.com/engine/network/port-publishing/
- The official PostgreSQL Docker image supports `POSTGRES_DB`,
  `POSTGRES_USER`, and `POSTGRES_PASSWORD` for first-run database and role
  initialization:
  https://hub.docker.com/_/postgres

## State Machine

### Local Startup

- States: doctor check, prerequisite check, local config loading, local Docker
  database check, local Docker database ready, missing config prompt, dependency
  restore, backend starting, backend failed with local database error, local
  database repair, backend retrying, backend healthy, frontend starting,
  running, failed.
- Events: command started, required command missing, `.env.local` read,
  environment variable found, user-secret found, PostgreSQL URL found, required
  value missing, Docker CLI found, Docker daemon reachable, project PostgreSQL
  container missing, project PostgreSQL container unhealthy, local database
  password failure, local database missing, local database privilege failure,
  Docker retry requested, manual PostgreSQL selected, quit selected, user
  submits value, user skips AI key, restore command fails, backend health check
  passes, backend exits, frontend exits.
- Guards: Node 20+ is required; `npm` and `dotnet` must be available for project
  dependency restoration; `ConnectionStrings__DefaultConnection` must have a
  real non-placeholder value before backend startup; missing local database
  config may be satisfied only by the project-owned Docker PostgreSQL database
  or by user-provided real PostgreSQL configuration; automatic database repair
  is allowed only for local database targets, not remote hosts.
- Transitions: missing database config moves to local Docker database check;
  missing or stopped Docker in an interactive terminal moves to Docker guidance
  and retry; explicit manual PostgreSQL selection moves to the PostgreSQL
  connection prompt; missing seed admin values move to local defaults; missing
  AI key moves to optional prompt; accepted or generated values move to
  dependency restore; local database startup failure moves to local database
  repair and one backend retry; backend health success moves to frontend
  startup; missing system commands, restore failure, remote database failure, or
  backend health timeout after retry move to failed.
- Side effects: write or update `.env.local` only; run `npm ci` when root
  dependencies are absent or the ignored local lockfile hash marker does not
  match `package-lock.json`; run `dotnet restore`; create/start/reuse Docker
  container `ojsharp-postgres-dev`; reset only `ojsharp-postgres-dev` and
  `ojsharp-postgres-dev-data` when that project-owned database is unhealthy;
  append backend logs to gitignored local log files; start backend and frontend
  child processes.
- Failure paths: missing non-interactive configuration fails with a clear list
  of variables; missing system runtimes fails with install guidance; backend
  startup failure reports the health URL, recent backend error log, and repair
  guidance for common PostgreSQL, Docker, and permission failures.
- Rollback path: restore `package.json` `dev` to the previous PowerShell command
  and remove `scripts/dev.mjs`, `scripts/dev-postgres.mjs`,
  `scripts/dev.test.mjs`, and this document.

### AI Local Configuration

- States: key configured, key missing, prompt shown, key stored, AI explicitly
  disabled.
- Events: `Deepseek__ApiKey` found in shell or `.env.local`, key missing, user
  enters key, user submits blank value, `Deepseek__Enabled=false` configured.
- Guards: frontend never receives provider keys; provider calls remain backend
  owned; blank key cannot be treated as a real AI provider.
- Transitions: existing key starts normally; entered key is stored in
  `.env.local` with `Deepseek__Enabled=true`; blank input stores
  `Deepseek__Enabled=false` and the backend returns structured provider
  unavailable errors for AI features.
- Side effects: write AI provider key only to `.env.local`, which remains
  untracked.
- Failure paths: non-interactive startup without a configured key fails unless
  AI is explicitly disabled.
- Rollback path: remove the prompt and return to manual user-secrets or hosting
  secret configuration only.

## Impact Surface

- Root npm scripts and lockfile metadata.
- Local startup scripts under `scripts/`.
- Local-only environment file workflow through `.env.local`.
- README, acceptance, TRD, and behavior-level test documentation.
- No change to production API contracts, database schema, RBAC, grading
  contracts, or frontend/provider isolation.

## Primitive Acceptance Criteria

- `npm run dev` uses a cross-platform startup script instead of a
  PowerShell-only command.
- `npm run dev:doctor` reports local readiness without starting servers,
  restoring dependencies, or writing secrets.
- On a fresh checkout with Node and npm available, the startup script installs
  root npm dependencies from `package-lock.json` when needed.
- Repeated startups skip root npm installation when the current
  `package-lock.json` hash already matches the ignored marker in `node_modules`.
- When `dotnet` is available, the startup script restores
  `Backend/Backend.sln` before starting the backend.
- If database configuration is missing and Docker is reachable, startup creates
  or reuses Docker container `ojsharp-postgres-dev` with database
  `aisd_ss26_group_7`, user `postgres`, password `postgres`, and the first free
  loopback host port from `55432` through `55449`.
- If database configuration is missing and Docker is unavailable in an
  interactive terminal, startup gives Docker install/start guidance and lets the
  user press Enter to retry before any manual PostgreSQL prompt is shown.
- Repeated startups reuse the same project-owned PostgreSQL container and named
  volume and do not create duplicate local database dependencies.
- If the project-owned PostgreSQL container exists but cannot become ready,
  startup resets only `ojsharp-postgres-dev` and `ojsharp-postgres-dev-data`.
- If a local PostgreSQL target fails backend startup because of bad password,
  missing database, missing role, insufficient privileges, or refused local
  connection, startup switches to the project-owned Docker PostgreSQL database
  and retries backend startup once.
- Remote PostgreSQL targets are not silently replaced by automatic local
  database repair.
- Missing seed administrator values use `admin@example.com` and `Admin123!`
  for local startup.
- Existing `ConnectionStrings__DefaultConnection`, `DATABASE_URL`, PG*
  environment variables, and matching .NET user-secrets are reused before
  prompting.
- PostgreSQL URLs such as `postgresql://user:password@host:5432/database` are
  normalized into Npgsql keyword connection strings before being written to
  `.env.local`.
- If `Deepseek__ApiKey` is missing and AI is not explicitly disabled, an
  interactive terminal prompts for a DeepSeek key; a blank response disables
  local AI instead of returning mock AI output.
- Startup writes secrets only to `.env.local` or process environment, never to
  tracked config files.
- The backend must become healthy before the frontend startup is handed to the
  user.
- Missing system runtimes or failed restores stop startup with explicit
  remediation guidance.
- Permission failures do not trigger unsafe automatic privilege escalation; the
  CLI gives exact authorization or grant steps and waits for the user or OS to
  complete them.
