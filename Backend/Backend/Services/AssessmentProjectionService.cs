using Backend.Domain;

namespace Backend.Services;

public sealed class AssessmentProjectionService
{
    public object ToStudentContext(Assessment assessment, AssessmentSession session)
    {
        return new
        {
            assessment_id = assessment.Id,
            title = assessment.Title,
            description = assessment.Description,
            duration_minutes = assessment.DurationMinutes,
            status = assessment.Status,
            ai_enabled = assessment.AiEnabled,
            ai_settings = ToAiSettings(assessment),
            reports_released = assessment.ReportsReleased,
            expires_at = session.ExpiresAt,
            ai_state = new
            {
                rescue_chances_remaining = session.RescueChancesRemaining,
                reflection_status = session.ReflectionStatus
            },
            questions = assessment.Questions
                .OrderBy(question => question.SortOrder)
                .Select(question => new
                {
                    question_id = question.Id,
                    title = question.Title,
                    problem_description_markdown = question.ProblemDescriptionMarkdown,
                    language_constraints = JsonDocumentSerializer.Deserialize(question.LanguageConstraintsJson, Array.Empty<string>()),
                    starter_code = JsonDocumentSerializer.Deserialize(question.StarterCodeJson, new Dictionary<string, string>()),
                    difficulty = question.Difficulty,
                    ai_credit_budget = ResolveAiCreditBudget(assessment, question)
                })
        };
    }

    public object ToAdminAssessment(Assessment assessment)
    {
        return new
        {
            assessment_id = assessment.Id,
            assessment.Title,
            assessment.Description,
            duration_minutes = assessment.DurationMinutes,
            assessment.Status,
            ai_enabled = assessment.AiEnabled,
            ai_settings = ToAiSettings(assessment),
            ai_credit_budget_override = assessment.AiCreditBudgetOverride,
            reports_released = assessment.ReportsReleased,
            question_count = assessment.Questions.Count,
            created_at = assessment.CreatedAt
        };
    }

    public object ToAdminAssessmentDetail(Assessment assessment)
    {
        return new
        {
            assessment_id = assessment.Id,
            assessment.Title,
            assessment.Description,
            duration_minutes = assessment.DurationMinutes,
            assessment.Status,
            ai_enabled = assessment.AiEnabled,
            ai_settings = ToAiSettings(assessment),
            ai_credit_budget_override = assessment.AiCreditBudgetOverride,
            reports_released = assessment.ReportsReleased,
            question_count = assessment.Questions.Count,
            questions = assessment.Questions
                .OrderBy(question => question.SortOrder)
                .Select(question => new
                {
                    question_id = question.Id,
                    question.Title,
                    problem_description_markdown = question.ProblemDescriptionMarkdown,
                    language_constraints = JsonDocumentSerializer.Deserialize(question.LanguageConstraintsJson, Array.Empty<string>()),
                    starter_code = JsonDocumentSerializer.Deserialize(question.StarterCodeJson, new Dictionary<string, string>()),
                    admin_notes = question.AdminNotes,
                    difficulty = question.Difficulty,
                    ai_credit_budget_override = question.AiCreditBudgetOverride,
                    ai_credit_budget = ResolveAiCreditBudget(assessment, question),
                    sort_order = question.SortOrder,
                    max_score = question.MaxScore,
                    admin_test_cases = question.TestCases
                        .OrderBy(testCase => testCase.Name)
                        .Select(testCase => new
                        {
                            test_case_id = testCase.Id,
                            testCase.Name,
                            testCase.Visibility,
                            test_code = JsonDocumentSerializer.Deserialize(testCase.TestCodeJson, new Dictionary<string, string>())
                        })
                })
        };
    }

    public static object ToAiSettings(Assessment assessment)
    {
        return new
        {
            structured_hints_enabled = assessment.StructuredHintsEnabled,
            ai_credits_enabled = assessment.AiCreditsEnabled,
            ai_rescue_enabled = assessment.AiRescueEnabled,
            reflection_enabled = assessment.ReflectionEnabled,
            rescue_correctness_probability = assessment.RescueCorrectnessProbability
        };
    }

    public static int ResolveAiCreditBudget(Assessment assessment, Question question)
    {
        if (question.AiCreditBudgetOverride is > 0)
        {
            return question.AiCreditBudgetOverride.Value;
        }

        if (assessment.AiCreditBudgetOverride is > 0)
        {
            return assessment.AiCreditBudgetOverride.Value;
        }

        return question.Difficulty switch
        {
            QuestionDifficulties.Easy => 6,
            QuestionDifficulties.Hard => 15,
            _ => 10
        };
    }
}
