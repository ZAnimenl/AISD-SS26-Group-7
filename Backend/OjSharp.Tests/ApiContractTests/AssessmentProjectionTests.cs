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
            SharedPrototypeReference = "product-dashboard",
            SharedPrototypeVersion = "v1",
            SharedPrototypeMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
            {
                ["workspace_root"] = "/workspace/product-dashboard"
            }),
            Questions =
            [
                new Question
                {
                    Id = Guid.NewGuid(),
                    Title = "Array Sum",
                    TaskType = TaskTypes.FrontendUiExtension,
                    VerificationMode = VerificationModes.BrowserUiPreview,
                    StarterPrototypeReference = "product-dashboard",
                    ProblemDescriptionMarkdown = "Solve it.",
                    AdminNotes = "Do not show this.",
                    LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "python" }),
                    StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["python"] = "def solve(arr):\n    pass\n"
                    }),
                    StarterFilesMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
                    {
                        ["python"] = new Dictionary<string, string>
                        {
                            ["main.py"] = "editable"
                        }
                    }),
                    VerificationMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["preview_route"] = "/preview"
                    }),
                    GradingConfigurationJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["hidden_runner"] = "pytest"
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
                            }),
                            AdminMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                            {
                                ["expected_output"] = "secret"
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
        Assert.Contains("frontend_ui_extension", json);
        Assert.Contains("browser_ui_preview", json);
        Assert.Contains("product-dashboard", json);
        Assert.Contains("preview_route", json);
        Assert.DoesNotContain("test_secret", json);
        Assert.DoesNotContain("hidden_runner", json);
        Assert.DoesNotContain("expected_output", json);
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
            SharedPrototypeReference = "product-dashboard",
            Questions =
            [
                new Question
                {
                    Id = Guid.NewGuid(),
                    Title = "Array Sum",
                    TaskType = TaskTypes.DatabaseQuerySchema,
                    VerificationMode = VerificationModes.DatabaseResultCheck,
                    ProblemDescriptionMarkdown = "Solve it.",
                    AdminNotes = "Visible to admin.",
                    LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "python", "javascript" }),
                    StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["python"] = "def solve(arr):\n    pass\n",
                        ["javascript"] = "function solve(arr) {\n}\n"
                    }),
                    SortOrder = 1,
                    MaxScore = 50,
                    GradingConfigurationJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["runner"] = "pytest"
                    }),
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
                            }),
                            AuthoringSource = AuthoringSources.AdminEdited,
                            AdminMetadataJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                            {
                                ["reviewed_by"] = "admin"
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
        Assert.Contains("database_query_schema", json);
        Assert.Contains("database_result_check", json);
        Assert.Contains("supported_task_categories", json);
        Assert.Contains("supported_verification_modes", json);
        Assert.Contains("Visible to admin.", json);
        Assert.Contains("test_secret", json);
        Assert.Contains("test_code", json);
        Assert.Contains("max_score", json);
        Assert.Contains("admin_test_cases", json);
        Assert.Contains("admin_edited", json);
        Assert.Contains("reviewed_by", json);
    }
}
