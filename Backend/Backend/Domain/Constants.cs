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
    public const string Passed = "passed";
    public const string Failed = "failed";
    public const string RuntimeError = "runtime_error";
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
