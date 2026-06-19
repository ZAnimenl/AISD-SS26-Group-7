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
            starts_at = assessment.StartsAt,
            status = assessment.Status,
            ai_enabled = assessment.AiEnabled,
            shared_prototype_reference = assessment.SharedPrototypeReference,
            shared_prototype_version = assessment.SharedPrototypeVersion,
            shared_prototype_metadata = JsonDocumentSerializer.Deserialize(assessment.SharedPrototypeMetadataJson, new Dictionary<string, string>()),
            expires_at = session.ExpiresAt,
            questions = assessment.Questions
                .OrderBy(question => question.SortOrder)
                .Select(question => new
                {
                    question_id = question.Id,
                    title = question.Title,
                    task_type = NormalizeTaskType(question.TaskType),
                    difficulty = question.Difficulty,
                    verification_mode = NormalizeVerificationMode(question.VerificationMode, question.TaskType),
                    starter_prototype_reference = question.StarterPrototypeReference,
                    problem_description_markdown = question.ProblemDescriptionMarkdown,
                    language_constraints = JsonDocumentSerializer.Deserialize(question.LanguageConstraintsJson, Array.Empty<string>()),
                    starter_code = JsonDocumentSerializer.DeserializeStarterCode(question.StarterCodeJson),
                    starter_files_metadata = JsonDocumentSerializer.Deserialize(question.StarterFilesMetadataJson, new Dictionary<string, Dictionary<string, string>>()),
                    verification_metadata = JsonDocumentSerializer.Deserialize(question.VerificationMetadataJson, new Dictionary<string, string>())
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
            starts_at = assessment.StartsAt,
            assessment.Status,
            ai_enabled = assessment.AiEnabled,
            shared_prototype_reference = assessment.SharedPrototypeReference,
            shared_prototype_version = assessment.SharedPrototypeVersion,
            shared_prototype_metadata = JsonDocumentSerializer.Deserialize(assessment.SharedPrototypeMetadataJson, new Dictionary<string, string>()),
            supported_task_categories = SupportedTaskCategories(),
            supported_verification_modes = SupportedVerificationModes(),
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
            starts_at = assessment.StartsAt,
            assessment.Status,
            ai_enabled = assessment.AiEnabled,
            shared_prototype_reference = assessment.SharedPrototypeReference,
            shared_prototype_version = assessment.SharedPrototypeVersion,
            shared_prototype_metadata = JsonDocumentSerializer.Deserialize(assessment.SharedPrototypeMetadataJson, new Dictionary<string, string>()),
            supported_task_categories = SupportedTaskCategories(),
            supported_verification_modes = SupportedVerificationModes(),
            question_count = assessment.Questions.Count,
            questions = assessment.Questions
                .OrderBy(question => question.SortOrder)
                .Select(question => new
                {
                    question_id = question.Id,
                    question.Title,
                    task_type = NormalizeTaskType(question.TaskType),
                    difficulty = question.Difficulty,
                    verification_mode = NormalizeVerificationMode(question.VerificationMode, question.TaskType),
                    starter_prototype_reference = question.StarterPrototypeReference,
                    problem_description_markdown = question.ProblemDescriptionMarkdown,
                    language_constraints = JsonDocumentSerializer.Deserialize(question.LanguageConstraintsJson, Array.Empty<string>()),
                    starter_code = JsonDocumentSerializer.DeserializeStarterCode(question.StarterCodeJson),
                    starter_files_metadata = JsonDocumentSerializer.Deserialize(question.StarterFilesMetadataJson, new Dictionary<string, Dictionary<string, string>>()),
                    verification_metadata = JsonDocumentSerializer.Deserialize(question.VerificationMetadataJson, new Dictionary<string, string>()),
                    grading_configuration = JsonDocumentSerializer.Deserialize(question.GradingConfigurationJson, new Dictionary<string, string>()),
                    authoring_source = question.AuthoringSource,
                    traceability_metadata = JsonDocumentSerializer.Deserialize(question.TraceabilityMetadataJson, new Dictionary<string, string>()),
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
                            test_code = JsonDocumentSerializer.Deserialize(testCase.TestCodeJson, new Dictionary<string, string>()),
                            authoring_source = testCase.AuthoringSource,
                            public_metadata = JsonDocumentSerializer.Deserialize(testCase.PublicMetadataJson, new Dictionary<string, string>()),
                            admin_metadata = JsonDocumentSerializer.Deserialize(testCase.AdminMetadataJson, new Dictionary<string, string>()),
                            traceability_metadata = JsonDocumentSerializer.Deserialize(testCase.TraceabilityMetadataJson, new Dictionary<string, string>())
                        })
                })
        };
    }

    private static string NormalizeTaskType(string? taskType)
    {
        return taskType switch
        {
            TaskTypes.FrontendUiExtension => TaskTypes.FrontendUiExtension,
            TaskTypes.RestApiDevelopment => TaskTypes.RestApiDevelopment,
            TaskTypes.DatabaseQuerySchema => TaskTypes.DatabaseQuerySchema,
            TaskTypes.BugFix => TaskTypes.BugFix,
            TaskTypes.LegacyWebApplication => TaskTypes.FrontendUiExtension,
            TaskTypes.LegacyApiDevelopment => TaskTypes.RestApiDevelopment,
            TaskTypes.LegacyDatabaseTask => TaskTypes.DatabaseQuerySchema,
            _ => TaskTypes.RestApiDevelopment
        };
    }

    private static string NormalizeVerificationMode(string? verificationMode, string? taskType)
    {
        if (verificationMode is VerificationModes.BrowserUiPreview
            or VerificationModes.ApiResponseCheck
            or VerificationModes.DatabaseResultCheck
            or VerificationModes.AutomatedTest
            or VerificationModes.RegressionTest)
        {
            return verificationMode;
        }

        return NormalizeTaskType(taskType) switch
        {
            TaskTypes.FrontendUiExtension => VerificationModes.BrowserUiPreview,
            TaskTypes.RestApiDevelopment => VerificationModes.ApiResponseCheck,
            TaskTypes.DatabaseQuerySchema => VerificationModes.DatabaseResultCheck,
            TaskTypes.BugFix => VerificationModes.RegressionTest,
            _ => VerificationModes.AutomatedTest
        };
    }

    private static string[] SupportedTaskCategories()
    {
        return
        [
            TaskTypes.FrontendUiExtension,
            TaskTypes.RestApiDevelopment,
            TaskTypes.DatabaseQuerySchema,
            TaskTypes.BugFix
        ];
    }

    private static string[] SupportedVerificationModes()
    {
        return
        [
            VerificationModes.BrowserUiPreview,
            VerificationModes.ApiResponseCheck,
            VerificationModes.DatabaseResultCheck,
            VerificationModes.AutomatedTest,
            VerificationModes.RegressionTest
        ];
    }
}
