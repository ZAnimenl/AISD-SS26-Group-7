namespace Backend.Services.Grading;

internal sealed class GradingTestFileFactory
{
    public void Write(string directory, Dictionary<string, string> files, string testCode, GradingLanguage language)
    {
        foreach (var (fileName, content) in files)
        {
            File.WriteAllText(Path.Combine(directory, fileName), content);
        }

        if (language == GradingLanguage.JavaScript || language == GradingLanguage.TypeScript)
        {
            File.WriteAllText(Path.Combine(directory, "solution.test.js"), testCode);
            return;
        }

        File.WriteAllText(Path.Combine(directory, "test_solution.py"), testCode);
    }
}
