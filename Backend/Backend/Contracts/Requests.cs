namespace Backend.Contracts;

public sealed record LoginRequest(string Email, string Password);

public sealed record RegisterRequest(string FullName, string Email, string Password);

public sealed record UserRequest(string FullName, string Email, string Password, string Role, string Status);

public sealed record UpdateUserRequest(string? FullName, string? Role, string? Status);

public sealed record AssessmentRequest(
    string Title,
    string Description,
    int DurationMinutes,
    string Status,
    bool AiEnabled,
    AssessmentAiSettingsRequest? AiSettings = null,
    int? AiCreditBudgetOverride = null,
    bool? ReportsReleased = null);

public sealed record AssessmentAiSettingsRequest(
    bool? StructuredHintsEnabled,
    bool? AiCreditsEnabled,
    bool? AiRescueEnabled,
    bool? ReflectionEnabled,
    double? RescueCorrectnessProbability);

public sealed record QuestionRequest(
    string Title,
    string ProblemDescriptionMarkdown,
    string[] LanguageConstraints,
    Dictionary<string, string> StarterCode,
    string? AdminNotes,
    int SortOrder,
    int MaxScore,
    string? Difficulty = null,
    int? AiCreditBudgetOverride = null);

public sealed record TestCaseRequest(string Name, string Visibility, Dictionary<string, string> TestCode);

public sealed record WorkspaceUpdateRequest(Dictionary<string, WorkspaceQuestionUpdateRequest> Questions);

public sealed record WorkspaceQuestionUpdateRequest(
    string SelectedLanguage,
    string ActiveFile,
    Dictionary<string, WorkspaceFileRequest> Files,
    int? Version);

public sealed record WorkspaceFileRequest(string Language, string Content);

public sealed record AssessmentRunCodeRequest(string SelectedLanguage, string ActiveFileContent);

public sealed record AssessmentAiChatRequest(
    string InteractionType,
    string Message,
    string SelectedLanguage,
    string ActiveFileContent);

public sealed record AssessmentAiHintRequest(
    string HintLevel,
    string? Message,
    string SelectedLanguage,
    string ActiveFileContent);

public sealed record ReflectionRequest(string? ReflectionText);
