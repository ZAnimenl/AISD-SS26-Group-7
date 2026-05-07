using Backend.Domain;

namespace Backend.Services;

public sealed class WorkspaceProjectionService
{
    public object ToWorkspace(Guid attemptId, IEnumerable<WorkspaceQuestionState> states)
    {
        return new
        {
            attempt_id = attemptId,
            questions = states.ToDictionary(
                state => state.QuestionId.ToString(),
                state => new
                {
                    selected_language = state.SelectedLanguage,
                    active_file = state.ActiveFile,
                    files = JsonDocumentSerializer.Deserialize(state.FilesJson, new Dictionary<string, WorkspaceFileDto>()),
                    last_saved_at = state.LastSavedAt,
                    state.Version
                })
        };
    }
}

public sealed record WorkspaceFileDto(string Language, string Content);
