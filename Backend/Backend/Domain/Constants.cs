namespace Backend.Domain;

public static class AssessmentStatuses
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Closed = "closed";
    public const string Archived = "archived";
}

public static class ExecutionStatuses
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Passed = "passed";
    public const string Failed = "failed";
    public const string RuntimeError = "runtime_error";
    public const string TimeLimitExceeded = "time_limit_exceeded";
    public const string MemoryLimitExceeded = "memory_limit_exceeded";
    public const string InternalError = "internal_error";
}

public static class SessionStatuses
{
    public const string NotStarted = "not_started";
    public const string Active = "active";
    public const string Expired = "expired";
    public const string Submitted = "submitted";
    public const string Closed = "closed";
}

public static class TestCaseVisibilities
{
    public const string Public = "public";
    public const string Hidden = "hidden";
}

public static class UserRoles
{
    public const string Student = "student";
    public const string Administrator = "administrator";
}

public static class UserStatuses
{
    public const string Active = "active";
    public const string Inactive = "inactive";
}

public static class AiInteractionTypes
{
    public const string CodeSuggestion = "code_suggestion";
    public const string Explanation = "explanation";
    public const string Debugging = "debugging";
}

public static class TaskTypes
{
    public const string FrontendUiExtension = "frontend_ui_extension";
    public const string RestApiDevelopment = "rest_api_development";
    public const string DatabaseQuerySchema = "database_query_schema";
    public const string BugFix = "bug_fix";

    public const string LegacyWebApplication = "web_application";
    public const string LegacyDatabaseTask = "database_task";
    public const string LegacyApiDevelopment = "api_development";
}

public static class VerificationModes
{
    public const string BrowserUiPreview = "browser_ui_preview";
    public const string ApiResponseCheck = "api_response_check";
    public const string DatabaseResultCheck = "database_result_check";
    public const string AutomatedTest = "automated_test";
    public const string RegressionTest = "regression_test";
}

public static class AuthoringSources
{
    public const string Manual = "manual";
    public const string LlmGenerated = "llm_generated";
    public const string AdminEdited = "admin_edited";
}
