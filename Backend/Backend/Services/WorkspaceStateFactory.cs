using Backend.Domain;

namespace Backend.Services;

public static class WorkspaceStateFactory
{
    public static WorkspaceQuestionState Create(Guid sessionId, Question question, DateTimeOffset now)
    {
        var starterCode = JsonDocumentSerializer.DeserializeStarterCode(question.StarterCodeJson);
        var language = SelectInitialLanguage(question, starterCode);
        var languageFiles = starterCode.GetValueOrDefault(language, new Dictionary<string, string>());
        var firstFile = languageFiles.Keys.FirstOrDefault() ?? GetActiveFile(language);
        var workspaceFiles = languageFiles.ToDictionary(
            entry => entry.Key,
            entry => new WorkspaceFileDto(language, entry.Value));

        if (workspaceFiles.Count == 0)
        {
            workspaceFiles[firstFile] = new WorkspaceFileDto(language, string.Empty);
        }

        return new WorkspaceQuestionState
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            QuestionId = question.Id,
            SelectedLanguage = language,
            ActiveFile = firstFile,
            FilesJson = JsonDocumentSerializer.Serialize(workspaceFiles),
            LastSavedAt = now,
            Version = 1
        };
    }

    internal static string SelectInitialLanguage(
        Question question,
        IReadOnlyDictionary<string, Dictionary<string, string>> starterCode)
    {
        var allowedLanguages = AssessmentPolicy.GetSupportedStudentLanguages(question);
        foreach (var language in allowedLanguages)
        {
            if (starterCode.ContainsKey(language))
            {
                return language;
            }
        }

        return allowedLanguages.FirstOrDefault()
               ?? starterCode.Keys.Select(AssessmentPolicy.NormalizeLanguage)
                   .FirstOrDefault(language => language is "python" or "javascript")
               ?? "python";
    }

    private static string GetActiveFile(string language)
    {
        return language switch
        {
            "javascript" => "main.js",
            "typescript" => "main.ts",
            _ => "main.py"
        };
    }
}
