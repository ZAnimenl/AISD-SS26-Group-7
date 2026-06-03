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
    public const string ReflectionPending = "reflection_pending";
    public const string Submitted = "submitted";
    public const string Closed = "closed";
}

public static class QuestionDifficulties
{
    public const string Easy = "easy";
    public const string Medium = "medium";
    public const string Hard = "hard";
}

public static class AiHintLevels
{
    public const string ConceptHint = "concept_hint";
    public const string StrategyHint = "strategy_hint";
    public const string DebuggingHint = "debugging_hint";
    public const string PseudocodeHint = "pseudocode_hint";
    public const string CodeLevelSuggestion = "code_level_suggestion";

    public static int DefaultCost(string hintLevel)
    {
        return hintLevel switch
        {
            ConceptHint => 1,
            StrategyHint => 2,
            DebuggingHint => 3,
            PseudocodeHint => 4,
            CodeLevelSuggestion => 6,
            _ => 1
        };
    }
}

public static class ReflectionStatuses
{
    public const string NotStarted = "not_started";
    public const string Pending = "pending";
    public const string Submitted = "submitted";
    public const string AutoSubmitted = "auto_submitted";
}

public static class RescueCorrectnessLabels
{
    public const string Correct = "correct";
    public const string Misleading = "misleading";
}

public static class RescueDecisions
{
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string Modified = "modified";
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
