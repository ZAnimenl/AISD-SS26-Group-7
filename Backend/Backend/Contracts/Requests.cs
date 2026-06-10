namespace Backend.Contracts;

public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);

public sealed record RegisterRequest(string FullName, string Email, string Password);

public sealed record VerifyEmailRequest(string Token);

public sealed record ResendVerificationRequest(string Email);

public sealed record GoogleLoginStartRequest(bool RememberMe = false);

// === Code-based registration ===
public sealed record RegisterStartRequest(string FullName, string Email);

public sealed record RegisterVerifyCodeRequest(string Email, string Code);

public sealed record RegisterCompleteRequest(string Email, string Code, string Password, bool RememberMe = false);

public sealed record RegisterResendCodeRequest(string Email);

// === Forgot password ===
public sealed record ForgotPasswordRequest(string Email);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

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

public sealed record GenerateQuestionDraftRequest(
    string TaskType,
    string Difficulty,
    string[] SupportedLanguages,
    string? StarterPrototypeReference = null);

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
    string ActiveFileContent,
    string? ActiveFileName = null,
    Dictionary<string, string>? VisibleFiles = null,
    AiRunContextRequest? LastRunResult = null);

public sealed record AiRunContextRequest(
    string Status,
    string? Stdout = null,
    string? Stderr = null,
    AiRunTestResultRequest[]? TestResults = null);

public sealed record AiRunTestResultRequest(
    string Name,
    bool Passed,
    string? Output = null);
