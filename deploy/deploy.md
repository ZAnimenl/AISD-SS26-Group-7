# Deployment Notes

## Publish configuration

When publishing the backend, do not put the seed administrator credentials in `appsettings.json`. The production `appsettings.json` intentionally omits the `SeedAdmin` section, so the published application must receive these values from environment variables.

## Seed administrator

The backend seeds or repairs the seed administrator from environment variables during database initialization.

Required variables:

```bash
SeedAdmin__Email=admin@example.com
SeedAdmin__Password=change-this-password
```

These values are read through ASP.NET Core configuration from the `SeedAdmin` section. In shell and container environments, use double underscores (`__`) to represent nested configuration keys.

If either value is missing in a published environment, the seed step fails validation and the administrator account is not created. Set these variables before starting the backend for the first time against an empty database.

Local debug runs use `appsettings.Development.json`, which keeps demo values for convenience:

```text
admin@example.com / password
```

The demo student account remains seeded as:

```text
student@example.com / password
```

## Database
The backend uses PostgreSQL as the database. Debug uses port `5433` from `appsettings.Development.json`. Production publish uses port `5432` from `appsettings.json` unless overridden by environment variables or deployment-specific configuration.
