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
            ("Username", "ALTER TABLE users ADD COLUMN Username TEXT NOT NULL DEFAULT '';"),
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

        if (!existing.Contains("Username"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE users SET Username = FullName WHERE Username = '';",
                cancellationToken);
        }

        var assessmentColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(assessments);";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                assessmentColumns.Add(reader.GetString(1));
            }
        }

        if (assessmentColumns.Count > 0 && !assessmentColumns.Contains("StartsAt"))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE assessments ADD COLUMN StartsAt TEXT NULL;",
                cancellationToken);
            logger.LogInformation("Added assessments.StartsAt column to local SQLite database.");
        }

        if (assessmentColumns.Count > 0)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "UPDATE assessments SET StartsAt = CreatedAt WHERE StartsAt IS NULL OR StartsAt = '';",
                cancellationToken);
        }

        var sessionColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(assessment_sessions);";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                sessionColumns.Add(reader.GetString(1));
            }
        }

        var sessionPending = new List<(string Name, string Sql)>
        {
            ("ReflectionText", "ALTER TABLE assessment_sessions ADD COLUMN ReflectionText TEXT NOT NULL DEFAULT '';"),
            ("ReflectionWordCount", "ALTER TABLE assessment_sessions ADD COLUMN ReflectionWordCount INTEGER NOT NULL DEFAULT 0;"),
            ("ReflectionDeadline", "ALTER TABLE assessment_sessions ADD COLUMN ReflectionDeadline TEXT NULL;"),
            ("ReflectionSubmittedAt", "ALTER TABLE assessment_sessions ADD COLUMN ReflectionSubmittedAt TEXT NULL;"),
            ("ReflectionSubmissionReason", "ALTER TABLE assessment_sessions ADD COLUMN ReflectionSubmissionReason TEXT NULL;"),
            ("AiGradingStatus", "ALTER TABLE assessment_sessions ADD COLUMN AiGradingStatus TEXT NOT NULL DEFAULT 'not_required';"),
            ("AiUsageScore", "ALTER TABLE assessment_sessions ADD COLUMN AiUsageScore INTEGER NULL;"),
            ("AiGradingDetailsJson", "ALTER TABLE assessment_sessions ADD COLUMN AiGradingDetailsJson jsonb NOT NULL DEFAULT '{{}}';"),
            ("AiGradingModel", "ALTER TABLE assessment_sessions ADD COLUMN AiGradingModel TEXT NULL;"),
            ("AiRubricVersion", "ALTER TABLE assessment_sessions ADD COLUMN AiRubricVersion TEXT NULL;"),
            ("AiGradingSummary", "ALTER TABLE assessment_sessions ADD COLUMN AiGradingSummary TEXT NULL;"),
            ("AiGradingConfidence", "ALTER TABLE assessment_sessions ADD COLUMN AiGradingConfidence TEXT NULL;"),
            ("AiGradedAt", "ALTER TABLE assessment_sessions ADD COLUMN AiGradedAt TEXT NULL;")
        };
        foreach (var (name, sql) in sessionPending)
        {
            if (!sessionColumns.Contains(name))
            {
                await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
                logger.LogInformation("Added assessment_sessions.{Column} column to local SQLite database.", name);
            }
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS ai_interaction_events (
                Id TEXT NOT NULL PRIMARY KEY,
                InteractionId TEXT NOT NULL,
                SessionId TEXT NOT NULL,
                EventType TEXT NOT NULL,
                ElapsedMilliseconds INTEGER NULL,
                AppliedUnchanged INTEGER NOT NULL DEFAULT 0,
                MetadataJson jsonb NOT NULL DEFAULT '{{}}',
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (InteractionId) REFERENCES ai_interactions (Id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS IX_ai_interaction_events_SessionId_CreatedAt
            ON ai_interaction_events (SessionId, CreatedAt);
            """,
            cancellationToken);
    }
}
