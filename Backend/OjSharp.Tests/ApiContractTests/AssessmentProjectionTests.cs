using System.Text.Json;
using Backend.Domain;
using Backend.Services;

namespace OjSharp.Tests.ApiContractTests;

public sealed class AssessmentProjectionTests
{
    [Fact]
    public void Student_context_does_not_include_hidden_test_case_data_or_admin_notes()
    {
        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            Title = "Assessment",
            Description = "Description",
            DurationMinutes = 60,
            Status = AssessmentStatuses.Active,
            AiEnabled = true,
            StructuredHintsEnabled = true,
            AiCreditsEnabled = true,
            AiRescueEnabled = true,
            ReflectionEnabled = true,
            RescueCorrectnessProbability = 0.5,
            Questions =
            [
                new Question
                {
                    Id = Guid.NewGuid(),
                    Title = "Array Sum",
                    ProblemDescriptionMarkdown = "Solve it.",
                    AdminNotes = "Do not show this.",
                    Difficulty = QuestionDifficulties.Easy,
                    LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "python" }),
                    StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["python"] = "def solve(arr):\n    pass\n"
                    }),
                    TestCases =
                    [
                        new TestCase
                        {
                            Name = "hidden",
                            Visibility = TestCaseVisibilities.Hidden,
                            TestCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                            {
                                ["python"] = "def test_secret():\n    assert False\n"
                            })
                        }
                    ]
                }
            ]
        };
        var session = new AssessmentSession
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessment.Id,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        var projection = new AssessmentProjectionService().ToStudentContext(assessment, session);
        var json = JsonSerializer.Serialize(projection, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        Assert.Contains("Array Sum", json);
        Assert.Contains("ai_settings", json);
        Assert.Contains("ai_credit_budget", json);
        Assert.Contains("rescue_chances_remaining", json);
        Assert.DoesNotContain("test_secret", json);
        Assert.DoesNotContain("Do not show this.", json);
    }

    [Fact]
    public void Admin_assessment_detail_includes_editable_question_and_test_case_data()
    {
        var assessment = new Assessment
        {
            Id = Guid.NewGuid(),
            Title = "Assessment",
            Description = "Description",
            DurationMinutes = 60,
            Status = AssessmentStatuses.Active,
            AiEnabled = true,
            StructuredHintsEnabled = true,
            AiCreditsEnabled = true,
            AiRescueEnabled = true,
            ReflectionEnabled = true,
            RescueCorrectnessProbability = 0.5,
            AiCreditBudgetOverride = 12,
            ReportsReleased = false,
            Questions =
            [
                new Question
                {
                    Id = Guid.NewGuid(),
                    Title = "Array Sum",
                    ProblemDescriptionMarkdown = "Solve it.",
                    AdminNotes = "Visible to admin.",
                    LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "python", "javascript" }),
                    StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["python"] = "def solve(arr):\n    pass\n",
                        ["javascript"] = "function solve(arr) {\n}\n"
                    }),
                    Difficulty = QuestionDifficulties.Hard,
                    AiCreditBudgetOverride = 14,
                    SortOrder = 1,
                    MaxScore = 50,
                    TestCases =
                    [
                        new TestCase
                        {
                            Id = Guid.NewGuid(),
                            Name = "hidden",
                            Visibility = TestCaseVisibilities.Hidden,
                            TestCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                            {
                                ["python"] = "def test_secret():\n    assert True\n"
                            })
                        }
                    ]
                }
            ]
        };

        var projection = new AssessmentProjectionService().ToAdminAssessmentDetail(assessment);
        var json = JsonSerializer.Serialize(projection, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        Assert.Contains("Array Sum", json);
        Assert.Contains("Visible to admin.", json);
        Assert.Contains("test_secret", json);
        Assert.Contains("test_code", json);
        Assert.Contains("max_score", json);
        Assert.Contains("admin_test_cases", json);
        Assert.Contains("difficulty", json);
        Assert.Contains("ai_credit_budget_override", json);
        Assert.Contains("reports_released", json);
        Assert.Contains("rescue_correctness_probability", json);
    }

    [Fact]
    public void Default_ai_credit_budget_uses_question_then_assessment_then_difficulty()
    {
        var assessment = new Assessment { AiCreditBudgetOverride = 11 };
        var easyQuestion = new Question { Difficulty = QuestionDifficulties.Easy };
        var overrideQuestion = new Question { Difficulty = QuestionDifficulties.Hard, AiCreditBudgetOverride = 7 };
        var defaultAssessment = new Assessment();

        Assert.Equal(11, AssessmentProjectionService.ResolveAiCreditBudget(assessment, easyQuestion));
        Assert.Equal(7, AssessmentProjectionService.ResolveAiCreditBudget(assessment, overrideQuestion));
        Assert.Equal(6, AssessmentProjectionService.ResolveAiCreditBudget(defaultAssessment, easyQuestion));
        Assert.Equal(10, AssessmentProjectionService.ResolveAiCreditBudget(defaultAssessment, new Question()));
        Assert.Equal(15, AssessmentProjectionService.ResolveAiCreditBudget(defaultAssessment, new Question { Difficulty = QuestionDifficulties.Hard }));
    }

    [Fact]
    public void Hint_credit_costs_match_updated_spec_defaults()
    {
        Assert.Equal(1, AiHintLevels.DefaultCost(AiHintLevels.ConceptHint));
        Assert.Equal(2, AiHintLevels.DefaultCost(AiHintLevels.StrategyHint));
        Assert.Equal(3, AiHintLevels.DefaultCost(AiHintLevels.DebuggingHint));
        Assert.Equal(4, AiHintLevels.DefaultCost(AiHintLevels.PseudocodeHint));
        Assert.Equal(6, AiHintLevels.DefaultCost(AiHintLevels.CodeLevelSuggestion));
    }
}
