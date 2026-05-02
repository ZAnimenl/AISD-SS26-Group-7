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
    bool AiEnabled);

public sealed record QuestionRequest(
    string Title,
    string ProblemDescriptionMarkdown,
    string[] LanguageConstraints,
    Dictionary<string, string> StarterCode,
    string? AdminNotes,
    int SortOrder,
    int MaxScore);

public sealed record TestCaseRequest(string Name, string Visibility, string Input, string ExpectedOutput);

public sealed record InitiateSessionRequest(Guid AssessmentId);

public sealed record WorkspaceUpdateRequest(Dictionary<string, WorkspaceQuestionUpdateRequest> Questions);

public sealed record WorkspaceQuestionUpdateRequest(
    string SelectedLanguage,
    string ActiveFile,
    Dictionary<string, WorkspaceFileRequest> Files,
    int? Version);

public sealed record WorkspaceFileRequest(string Language, string Content);

public sealed record RunCodeRequest(
    Guid SessionId,
    Guid AssessmentId,
    Guid QuestionId,
    string SelectedLanguage,
    string ActiveFileContent);

public sealed record FinalizeSubmissionRequest(Guid SessionId);

public sealed record AiChatRequest(
    Guid SessionId,
    Guid AssessmentId,
    Guid QuestionId,
    string InteractionType,
    string Message,
    string SelectedLanguage,
    string ActiveFileContent);
