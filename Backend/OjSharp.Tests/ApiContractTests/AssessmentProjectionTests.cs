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
            Questions =
            [
                new Question
                {
                    Id = Guid.NewGuid(),
                    Title = "Array Sum",
                    ProblemDescriptionMarkdown = "Solve it.",
                    AdminNotes = "Do not show this.",
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
                            Input = "secret input",
                            ExpectedOutput = "secret output"
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
        Assert.DoesNotContain("secret input", json);
        Assert.DoesNotContain("secret output", json);
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
                    SortOrder = 1,
                    MaxScore = 50,
                    TestCases =
                    [
                        new TestCase
                        {
                            Id = Guid.NewGuid(),
                            Name = "hidden",
                            Visibility = TestCaseVisibilities.Hidden,
                            Input = "secret input",
                            ExpectedOutput = "secret output"
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
        Assert.Contains("secret input", json);
        Assert.Contains("secret output", json);
        Assert.Contains("max_score", json);
        Assert.Contains("admin_test_cases", json);
    }
}
