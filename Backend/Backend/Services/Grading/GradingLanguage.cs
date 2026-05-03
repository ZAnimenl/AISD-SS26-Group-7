namespace Backend.Services.Grading;

internal enum GradingLanguage
{
    Python,
    JavaScript,
    TypeScript
}

internal static class GradingLanguageParser
{
    public static bool TryParse(string value, out GradingLanguage language)
    {
        if (value.Equals("javascript", StringComparison.OrdinalIgnoreCase)
            || value.Equals("js", StringComparison.OrdinalIgnoreCase))
        {
            language = GradingLanguage.JavaScript;
            return true;
        }

        if (value.Equals("typescript", StringComparison.OrdinalIgnoreCase)
            || value.Equals("ts", StringComparison.OrdinalIgnoreCase))
        {
            language = GradingLanguage.TypeScript;
            return true;
        }

        if (value.Equals("python", StringComparison.OrdinalIgnoreCase)
            || value.Equals("py", StringComparison.OrdinalIgnoreCase))
        {
            language = GradingLanguage.Python;
            return true;
        }

        language = default;
        return false;
    }
}
