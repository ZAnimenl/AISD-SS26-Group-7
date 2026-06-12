using Backend.Domain;

namespace Backend.Services;

public static class AssessmentPolicy
{
    private static readonly string[] DefaultStudentLanguages = ["python", "javascript"];
    private static readonly string[] SupportedStudentLanguages = ["python", "javascript", "typescript", "html", "sql"];

    public static bool IsAssessmentActive(Assessment? assessment)
    {
        return assessment?.Status == AssessmentStatuses.Active;
    }

    public static string[] GetSupportedStudentLanguages(Question question)
    {
        var configuredLanguages = JsonDocumentSerializer.Deserialize(question.LanguageConstraintsJson, Array.Empty<string>());
        var supportedLanguages = configuredLanguages
            .Select(NormalizeLanguage)
            .Where(IsSupportedStudentLanguage)
            .Distinct()
            .ToArray();

        var effectiveLanguages = supportedLanguages.Length > 0
            ? supportedLanguages
            : GetDefaultStudentLanguages(question.TaskType);

        if (question.TaskType == TaskTypes.FrontendUiExtension && !effectiveLanguages.Contains("html"))
        {
            return ["html"];
        }

        if (question.TaskType == TaskTypes.DatabaseQuerySchema && !effectiveLanguages.Contains("sql"))
        {
            return ["sql"];
        }

        return effectiveLanguages;
    }

    public static bool IsStudentLanguageAllowed(Question question, string? language)
    {
        var normalizedLanguage = NormalizeLanguage(language);
        return IsSupportedStudentLanguage(normalizedLanguage)
               && GetSupportedStudentLanguages(question).Contains(normalizedLanguage);
    }

    public static string GetDefaultFileName(string language)
    {
        return NormalizeLanguage(language) switch
        {
            "javascript" => "main.js",
            "typescript" => "main.ts",
            "html" => "index.html",
            "sql" => "solution.sql",
            _ => "main.py"
        };
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
        return string.IsNullOrWhiteSpace(language)
            ? string.Empty
            : language.Trim().ToLowerInvariant() switch
            {
                "js" => "javascript",
                "ts" => "typescript",
                "py" => "python",
                _ => language.Trim().ToLowerInvariant()
            };
    }

    private static string[] GetDefaultStudentLanguages(string? taskType)
    {
        return taskType switch
        {
            TaskTypes.FrontendUiExtension => ["html"],
            TaskTypes.DatabaseQuerySchema => ["sql"],
            _ => DefaultStudentLanguages
        };
    }

    private static bool IsSupportedStudentLanguage(string language)
    {
        return SupportedStudentLanguages.Contains(language);
    }
}
