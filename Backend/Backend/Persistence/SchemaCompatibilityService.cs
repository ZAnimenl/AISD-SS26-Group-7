using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public sealed class SchemaCompatibilityService(OjSharpDbContext dbContext)
{
    public async Task EnsureAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            DO $$
            BEGIN
                ALTER TABLE test_cases ADD COLUMN IF NOT EXISTS test_code_json text NOT NULL DEFAULT '{{}}';

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
}
