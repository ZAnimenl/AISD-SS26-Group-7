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
            expires_at = session.ExpiresAt,
            questions = assessment.Questions
                .OrderBy(question => question.SortOrder)
                .Select(question => new
                {
                    question_id = question.Id,
                    title = question.Title,
                    problem_description_markdown = question.ProblemDescriptionMarkdown,
                    language_constraints = JsonDocumentSerializer.Deserialize(question.LanguageConstraintsJson, Array.Empty<string>()),
                    starter_code = JsonDocumentSerializer.Deserialize(question.StarterCodeJson, new Dictionary<string, string>())
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
}
