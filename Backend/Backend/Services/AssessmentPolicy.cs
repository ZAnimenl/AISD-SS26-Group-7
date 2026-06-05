using Backend.Domain;

namespace Backend.Services;

public static class AssessmentPolicy
{
    private static readonly string[] DefaultStudentLanguages = ["python", "javascript"];

    public static bool IsAssessmentActive(Assessment? assessment)
    {
        return assessment?.Status == AssessmentStatuses.Active;
    }

    public static string[] GetSupportedStudentLanguages(Question question)
    {
        var configuredLanguages = JsonDocumentSerializer.Deserialize(question.LanguageConstraintsJson, Array.Empty<string>());
        var supportedLanguages = configuredLanguages
            .Select(NormalizeLanguage)
            .Where(language => language is "python" or "javascript")
            .Distinct()
            .ToArray();

        return supportedLanguages.Length > 0 ? supportedLanguages : DefaultStudentLanguages;
    }

    public static bool IsStudentLanguageAllowed(Question question, string? language)
    {
        var normalizedLanguage = NormalizeLanguage(language);
        return normalizedLanguage is "python" or "javascript"
            && GetSupportedStudentLanguages(question).Contains(normalizedLanguage);
    }

    public static bool TryFindUnsupportedWorkspaceLanguage(
        Question question,
        string selectedLanguage,
        IEnumerable<string> fileLanguages,
        out string unsupportedLanguage)
    {
        if (!IsStudentLanguageAllowed(question, selectedLanguage))
        {
            unsupportedLanguage = selectedLanguage;
            return true;
        }

        foreach (var fileLanguage in fileLanguages.Select(NormalizeLanguage))
        {
            if (!IsStudentLanguageAllowed(question, fileLanguage))
            {
                unsupportedLanguage = fileLanguage;
                return true;
            }
        }

        unsupportedLanguage = string.Empty;
        return false;
    }

    public static string NormalizeLanguage(string? language)
    {
        return string.IsNullOrWhiteSpace(language) ? string.Empty : language.Trim().ToLowerInvariant();
    }
}
