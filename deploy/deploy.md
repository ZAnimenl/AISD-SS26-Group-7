# Deployment Notes

## Seed administrator

The backend seeds the first administrator from environment variables during database initialization.

Required variables:

```bash
SeedAdmin__Email=admin@example.com
SeedAdmin__Password=change-this-password
```

These values are read through ASP.NET Core configuration from the `SeedAdmin` section. In shell and container environments, use double underscores (`__`) to represent nested configuration keys.

If either value is missing, the seed step fails validation and the administrator account is not created. Set these variables before starting the backend for the first time against an empty database.

The demo student account remains seeded as:

```text
student@example.com / password
```

## Database
The backend uses PostgreSQL as the database. The connection string is configured in `appsettings.json`. As `"DefaultConnection": "Host=localhost:5433;Database=ai_coding;Username=ai_coding;password=password"` describes the connection string, make sure the PostgreSQL server is running and accessible at the specified host and port, and that the database and credentials are set up accordingly.

