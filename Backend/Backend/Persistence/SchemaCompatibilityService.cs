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
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS structured_hints_enabled boolean NOT NULL DEFAULT true;
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS ai_credits_enabled boolean NOT NULL DEFAULT true;
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS ai_rescue_enabled boolean NOT NULL DEFAULT true;
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS reflection_enabled boolean NOT NULL DEFAULT true;
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS rescue_correctness_probability double precision NOT NULL DEFAULT 0.5;
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS ai_credit_budget_override integer NULL;
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS reports_released boolean NOT NULL DEFAULT false;
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS "StructuredHintsEnabled" boolean NOT NULL DEFAULT true;
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS "AiCreditsEnabled" boolean NOT NULL DEFAULT true;
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS "AiRescueEnabled" boolean NOT NULL DEFAULT true;
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS "ReflectionEnabled" boolean NOT NULL DEFAULT true;
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS "RescueCorrectnessProbability" double precision NOT NULL DEFAULT 0.5;
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS "AiCreditBudgetOverride" integer NULL;
                ALTER TABLE assessments ADD COLUMN IF NOT EXISTS "ReportsReleased" boolean NOT NULL DEFAULT false;

                ALTER TABLE questions ADD COLUMN IF NOT EXISTS difficulty character varying(64) NOT NULL DEFAULT 'medium';
                ALTER TABLE questions ADD COLUMN IF NOT EXISTS ai_credit_budget_override integer NULL;
                ALTER TABLE questions ADD COLUMN IF NOT EXISTS "Difficulty" character varying(64) NOT NULL DEFAULT 'medium';
                ALTER TABLE questions ADD COLUMN IF NOT EXISTS "AiCreditBudgetOverride" integer NULL;

                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS rescue_chances_remaining integer NOT NULL DEFAULT 4;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS reflection_status character varying(64) NOT NULL DEFAULT 'not_started';
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS reflection_started_at timestamp with time zone NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS reflection_expires_at timestamp with time zone NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS reflection_submitted_at timestamp with time zone NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS reflection_text text NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS reflection_evaluation_json jsonb NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS code_correctness_score integer NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS ai_usage_quality_score integer NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS reflection_understanding_score integer NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS critical_ai_judgment_score integer NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS process_aware_score integer NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS process_score_explanation_json jsonb NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS "RescueChancesRemaining" integer NOT NULL DEFAULT 4;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS "ReflectionStatus" character varying(64) NOT NULL DEFAULT 'not_started';
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS "ReflectionStartedAt" timestamp with time zone NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS "ReflectionExpiresAt" timestamp with time zone NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS "ReflectionSubmittedAt" timestamp with time zone NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS "ReflectionText" text NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS "ReflectionEvaluationJson" jsonb NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS "CodeCorrectnessScore" integer NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS "AiUsageQualityScore" integer NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS "ReflectionUnderstandingScore" integer NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS "CriticalAiJudgmentScore" integer NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS "ProcessAwareScore" integer NULL;
                ALTER TABLE assessment_sessions ADD COLUMN IF NOT EXISTS "ProcessScoreExplanationJson" jsonb NULL;

                ALTER TABLE ai_interactions ADD COLUMN IF NOT EXISTS hint_level character varying(64) NULL;
                ALTER TABLE ai_interactions ADD COLUMN IF NOT EXISTS credit_cost integer NOT NULL DEFAULT 0;
                ALTER TABLE ai_interactions ADD COLUMN IF NOT EXISTS is_rescue boolean NOT NULL DEFAULT false;
                ALTER TABLE ai_interactions ADD COLUMN IF NOT EXISTS rescue_correctness_label character varying(64) NULL;
                ALTER TABLE ai_interactions ADD COLUMN IF NOT EXISTS rescue_decision character varying(64) NULL;
                ALTER TABLE ai_interactions ADD COLUMN IF NOT EXISTS rescue_decision_time_ms integer NULL;
                ALTER TABLE ai_interactions ADD COLUMN IF NOT EXISTS "HintLevel" character varying(64) NULL;
                ALTER TABLE ai_interactions ADD COLUMN IF NOT EXISTS "CreditCost" integer NOT NULL DEFAULT 0;
                ALTER TABLE ai_interactions ADD COLUMN IF NOT EXISTS "IsRescue" boolean NOT NULL DEFAULT false;
                ALTER TABLE ai_interactions ADD COLUMN IF NOT EXISTS "RescueCorrectnessLabel" character varying(64) NULL;
                ALTER TABLE ai_interactions ADD COLUMN IF NOT EXISTS "RescueDecision" character varying(64) NULL;
                ALTER TABLE ai_interactions ADD COLUMN IF NOT EXISTS "RescueDecisionTimeMs" integer NULL;

                ALTER TABLE workspace_question_states ADD COLUMN IF NOT EXISTS ai_credits_remaining integer NULL;
                ALTER TABLE workspace_question_states ADD COLUMN IF NOT EXISTS "AiCreditsRemaining" integer NULL;

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
