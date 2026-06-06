namespace Backend.Services.Grading;

internal sealed class GradingTestFileFactory
{
    public void Write(string directory, Dictionary<string, string> files, string testCode, GradingLanguage language)
    {
        foreach (var (fileName, content) in files)
        {
            File.WriteAllText(Path.Combine(directory, fileName), content);
            WriteLegacyImportAlias(directory, fileName, content);
        }

        if (language == GradingLanguage.JavaScript || language == GradingLanguage.TypeScript)
        {
            File.WriteAllText(Path.Combine(directory, "solution.test.js"), testCode);
            return;
        }

        File.WriteAllText(Path.Combine(directory, "test_solution.py"), testCode);
    }

    private static void WriteLegacyImportAlias(string directory, string fileName, string content)
    {
        if (fileName != Path.GetFileName(fileName) || !fileName.Contains('_', StringComparison.Ordinal))
        {
            return;
        }

        var extension = Path.GetExtension(fileName);
        if (extension is not ".py" and not ".js")
        {
            return;
        }

        var alias = ToPascalCase(Path.GetFileNameWithoutExtension(fileName)) + extension;
        if (alias == fileName)
        {
            return;
        }

        var aliasPath = Path.Combine(directory, alias);
        if (!File.Exists(aliasPath))
        {
            File.WriteAllText(aliasPath, content);
        }
    }

    private static string ToPascalCase(string value)
    {
        return string.Concat(value
            .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..]));
    }
}
