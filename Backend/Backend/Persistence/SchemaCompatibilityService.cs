using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public sealed class SchemaCompatibilityService(OjSharpDbContext dbContext)
{
    private static readonly ConcurrentDictionary<string, CompatibilityState> States = new();

    public async Task EnsureAsync(CancellationToken cancellationToken)
    {
        var state = States.GetOrAdd(GetStateKey(), _ => new CompatibilityState());
        if (Volatile.Read(ref state.Ensured))
        {
            return;
        }

        await state.Gate.WaitAsync(cancellationToken);
        try
        {
            if (Volatile.Read(ref state.Ensured))
            {
                return;
            }

            await EnsureCoreAsync(cancellationToken);
            Volatile.Write(ref state.Ensured, true);
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private async Task EnsureCoreAsync(CancellationToken cancellationToken)
    {
        if (DatabaseProviders.IsSqliteProviderName(dbContext.Database.ProviderName))
        {
            return;
        }

        await using var schemaLock = await DatabaseAdvisoryLocks.AcquireSessionLockAsync(
            dbContext,
            DatabaseAdvisoryLocks.SchemaCompatibility,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DO $$
            BEGIN
                ALTER TABLE questions ADD COLUMN IF NOT EXISTS "TaskType" character varying(80) NOT NULL DEFAULT 'rest_api_development';
                ALTER TABLE questions ADD COLUMN IF NOT EXISTS "Difficulty" character varying(40) NOT NULL DEFAULT 'medium';
                ALTER TABLE questions ADD COLUMN IF NOT EXISTS "VerificationMode" character varying(80) NOT NULL DEFAULT 'api_response_check';
                ALTER TABLE questions ADD COLUMN IF NOT EXISTS "StarterPrototypeReference" character varying(200);
                ALTER TABLE questions ADD COLUMN IF NOT EXISTS "StarterFilesMetadataJson" jsonb NOT NULL DEFAULT '{{}}'::jsonb;
                ALTER TABLE questions ADD COLUMN IF NOT EXISTS "VerificationMetadataJson" jsonb NOT NULL DEFAULT '{{}}'::jsonb;
                ALTER TABLE questions ADD COLUMN IF NOT EXISTS "GradingConfigurationJson" jsonb NOT NULL DEFAULT '{{}}'::jsonb;
                ALTER TABLE questions ADD COLUMN IF NOT EXISTS "AuthoringSource" character varying(80) NOT NULL DEFAULT 'manual';
                ALTER TABLE questions ADD COLUMN IF NOT EXISTS "TraceabilityMetadataJson" jsonb NOT NULL DEFAULT '{{}}'::jsonb;
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS "SharedPrototypeReference" character varying(200);
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS "SharedPrototypeVersion" character varying(80);
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS "SharedPrototypeMetadataJson" jsonb NOT NULL DEFAULT '{{}}'::jsonb;
                ALTER TABLE test_cases ADD COLUMN IF NOT EXISTS test_code_json text NOT NULL DEFAULT '{{}}';
                ALTER TABLE test_cases ADD COLUMN IF NOT EXISTS "AuthoringSource" character varying(80) NOT NULL DEFAULT 'manual';
                ALTER TABLE test_cases ADD COLUMN IF NOT EXISTS "TraceabilityMetadataJson" jsonb NOT NULL DEFAULT '{{}}'::jsonb;
                ALTER TABLE test_cases ADD COLUMN IF NOT EXISTS "PublicMetadataJson" jsonb NOT NULL DEFAULT '{{}}'::jsonb;
                ALTER TABLE test_cases ADD COLUMN IF NOT EXISTS "AdminMetadataJson" jsonb NOT NULL DEFAULT '{{}}'::jsonb;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS "Username" character varying(80) NOT NULL DEFAULT '';

                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='test_cases' AND column_name='Input') THEN
                    ALTER TABLE test_cases ALTER COLUMN "Input" DROP NOT NULL;
                    ALTER TABLE test_cases ALTER COLUMN "Input" SET DEFAULT '';
                END IF;

                IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='test_cases' AND column_name='ExpectedOutput') THEN
                    ALTER TABLE test_cases ALTER COLUMN "ExpectedOutput" DROP NOT NULL;
                    ALTER TABLE test_cases ALTER COLUMN "ExpectedOutput" SET DEFAULT '';
                END IF;
            END $$;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            UPDATE assessment_sessions
            SET "Status" = 'expired'
            WHERE "Status" = 'active'
              AND "ExpiresAt" <= now();

            WITH ranked_active_sessions AS (
                SELECT
                    "Id",
                    row_number() OVER (
                        PARTITION BY "AssessmentId", "UserId"
                        ORDER BY "StartedAt" DESC, "Id" DESC
                    ) AS row_number
                FROM assessment_sessions
                WHERE "Status" = 'active'
            )
            UPDATE assessment_sessions
            SET "Status" = 'expired'
            WHERE "Id" IN (
                SELECT "Id"
                FROM ranked_active_sessions
                WHERE row_number > 1
            );

            WITH ranked_workspace_states AS (
                SELECT
                    "Id",
                    row_number() OVER (
                        PARTITION BY "SessionId", "QuestionId"
                        ORDER BY "Version" DESC, "LastSavedAt" DESC, "Id" DESC
                    ) AS row_number
                FROM workspace_question_states
            )
            DELETE FROM workspace_question_states
            WHERE "Id" IN (
                SELECT "Id"
                FROM ranked_workspace_states
                WHERE row_number > 1
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_assessment_sessions_one_active_per_user_assessment"
            ON assessment_sessions ("AssessmentId", "UserId")
            WHERE "Status" = 'active';

            CREATE INDEX IF NOT EXISTS "IX_assessment_sessions_AssessmentId_UserId_Status"
            ON assessment_sessions ("AssessmentId", "UserId", "Status");

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_workspace_question_states_SessionId_QuestionId"
            ON workspace_question_states ("SessionId", "QuestionId");

            UPDATE users
            SET "Username" = "FullName"
            WHERE "Username" IS NULL OR "Username" = '';
            """,
            cancellationToken);

        var emptyJson = "{}";
        var defaultTestCodeJson = JsonSerializer.Serialize(DefaultTestCode());
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE test_cases
            SET test_code_json = {defaultTestCodeJson}
            WHERE test_code_json IS NULL OR test_code_json = {emptyJson};
            """,
            cancellationToken);
    }

    private static Dictionary<string, string> DefaultTestCode()
    {
        return new Dictionary<string, string>
        {
            ["python"] = "from solution import solve\n\n\ndef test_solution_exists():\n    assert callable(solve)\n",
            ["javascript"] = "const { solve } = require(\"./solution.js\");\n\ntest(\"solution exists\", () => {\n  expect(typeof solve).toBe(\"function\");\n});\n",
            ["typescript"] = "const solve = globalThis.__ojsharpSolve;\n\ntest(\"solution exists\", () => {\n  expect(typeof solve).toBe(\"function\");\n});\n"
        };
    }

    private string GetStateKey()
    {
        var connectionString = dbContext.Database.GetConnectionString();
        return string.IsNullOrWhiteSpace(connectionString)
            ? dbContext.Database.ProviderName ?? nameof(OjSharpDbContext)
            : connectionString;
    }

    private sealed class CompatibilityState
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public bool Ensured;
    }
}
