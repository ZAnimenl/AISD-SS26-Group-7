# Deployment Notes

## Publish configuration

Do not put database credentials or the seed administrator credentials in tracked `appsettings*.json` files. The backend must receive these values from environment variables, user secrets, or the hosting secret manager before it starts. The current private course checkout contains dev-only Google OAuth and SMTP values in `Backend/Backend/appsettings.Development.json`; rotate/remove those values before public release or production deployment.

## Seed administrator

The backend seeds or repairs the seed administrator from environment variables during database initialization.

Required variables:

```bash
SeedAdmin__Email=<real-admin-email>
SeedAdmin__Password=<real-initial-admin-password>
```

These values are read through ASP.NET Core configuration from the `SeedAdmin` section. In shell and container environments, use double underscores (`__`) to represent nested configuration keys.

If either value is missing, the seed step fails validation and backend startup fails. Set these variables before starting the backend for the first time against an empty database.

## Database
Local one-command startup uses SQLite through EF Core. Production and explicit
external deployments should use PostgreSQL through EF Core. Deployment
environments must set:

```bash
ConnectionStrings__DefaultConnection=Host=...;Database=...;Username=...;Password=...
```

If the connection string is missing or database initialization fails, startup fails instead of serving a partial deployment.

## Frontend API target

Production frontend deployments must set:

```bash
NEXT_PUBLIC_API_BASE_URL=https://your-backend.example.com/api/v1
```

The frontend uses localhost fallback URLs only for local development.

## AI provider

AI assistance and LLM draft generation require a configured provider that returns content and token usage. Missing or failing providers return structured API errors such as `AI_PROVIDER_UNAVAILABLE`; the backend does not return mock guidance or template drafts as generated output.

## Sandbox execution

Run and submit require a real Docker-compatible container runtime for the backend `DockerCodeRunner`.

For a lightweight local deployment on macOS, Colima can provide the Docker socket:

```bash
colima start --cpu 2 --memory 2 --disk 20 --runtime docker
DOCKER_HOST=unix://$HOME/.colima/default/docker.sock
```

If `DOCKER_HOST` is not set for Colima, the backend will look for the default Docker socket and run/submit will fail closed with a dependency error instead of fabricating results.

## Seed data

Backend startup seeds or repairs only the configured administrator account. It does not create demo users, demo assessments, or demo prototype content in any environment.
