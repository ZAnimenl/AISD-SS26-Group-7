using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

/// <summary>Idempotent SQLite schema patches for auth columns that were added after the
/// initial EnsureCreated baseline. Teammates who have an older local SQLite file will
/// otherwise hit "no such column: u.AuthProvider" because EF Core EnsureCreated does
/// not alter existing tables.</summary>
public sealed class SqliteAuthSchemaMigrator(OjSharpDbContext dbContext, ILogger<SqliteAuthSchemaMigrator> logger)
{
    public async Task EnsureAsync(CancellationToken cancellationToken)
    {
        if (!DatabaseProviders.IsSqliteProviderName(dbContext.Database.ProviderName))
        {
            return;
        }

        // PRAGMA table_info returns a row per existing column.
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var connection = dbContext.Database.GetDbConnection();
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(users);";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                // PRAGMA table_info columns: cid, name, type, notnull, dflt_value, pk
                existing.Add(reader.GetString(1));
            }
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 1)
        {
            // "no such table" — first run, EnsureCreated will create everything.
            return;
        }

        if (existing.Count == 0)
        {
            return;
        }

        var pending = new List<(string Name, string Sql)>
        {
            ("AuthProvider", "ALTER TABLE users ADD COLUMN AuthProvider TEXT NOT NULL DEFAULT 'email';"),
            ("GoogleId", "ALTER TABLE users ADD COLUMN GoogleId TEXT NULL;"),
            ("EmailVerified", "ALTER TABLE users ADD COLUMN EmailVerified INTEGER NOT NULL DEFAULT 0;"),
            ("EmailVerificationToken", "ALTER TABLE users ADD COLUMN EmailVerificationToken TEXT NULL;"),
            ("EmailVerificationTokenExpiresAt", "ALTER TABLE users ADD COLUMN EmailVerificationTokenExpiresAt TEXT NULL;"),
            ("MustChangePassword", "ALTER TABLE users ADD COLUMN MustChangePassword INTEGER NOT NULL DEFAULT 0;")
        };

        foreach (var (name, sql) in pending)
        {
            if (existing.Contains(name))
            {
                continue;
            }

            try
            {
                await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                logger.LogInformation("Added users.{Column} column to local SQLite database.", name);
            }
            catch (SqliteException exception) when (exception.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            {
                // Concurrent startup or partial migration — column already exists, ignore.
            }
        }

        // After backfilling AuthProvider, mark any pre-existing accounts as email-verified so
        // teammates who already had a seeded admin / test users can still log in without going
        // through the new verification flow.
        if (!existing.Contains("EmailVerified"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE users SET EmailVerified = 1 WHERE EmailVerified = 0;",
                cancellationToken);
        }
    }
}
