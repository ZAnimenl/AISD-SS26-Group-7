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
    string? SharedPrototypeReference = null,
    string? SharedPrototypeVersion = null,
    Dictionary<string, string>? SharedPrototypeMetadata = null);

public sealed record QuestionRequest(
    string Title,
    string ProblemDescriptionMarkdown,
    string[] LanguageConstraints,
    Dictionary<string, Dictionary<string, string>> StarterCode,
    string? AdminNotes,
    int SortOrder,
    int MaxScore,
    string? TaskType = null,
    string? Difficulty = null,
    string? VerificationMode = null,
    string? StarterPrototypeReference = null,
    Dictionary<string, Dictionary<string, string>>? StarterFilesMetadata = null,
    Dictionary<string, string>? VerificationMetadata = null,
    Dictionary<string, string>? GradingConfiguration = null,
    string? AuthoringSource = null,
    Dictionary<string, string>? TraceabilityMetadata = null);

public sealed record TestCaseRequest(
    string Name,
    string Visibility,
    Dictionary<string, string> TestCode,
    string? AuthoringSource = null,
    Dictionary<string, string>? PublicMetadata = null,
    Dictionary<string, string>? AdminMetadata = null,
    Dictionary<string, string>? TraceabilityMetadata = null);

public sealed record WorkspaceUpdateRequest(Dictionary<string, WorkspaceQuestionUpdateRequest> Questions);

public sealed record WorkspaceQuestionUpdateRequest(
    string SelectedLanguage,
    string ActiveFile,
    Dictionary<string, WorkspaceFileRequest> Files,
    int? Version);

public sealed record WorkspaceFileRequest(string Language, string Content);

public sealed record AssessmentRunCodeRequest(string SelectedLanguage, Dictionary<string, string> Files);

public sealed record AssessmentAiChatRequest(
    string InteractionType,
    string Message,
    string SelectedLanguage,
    string ActiveFileContent);
