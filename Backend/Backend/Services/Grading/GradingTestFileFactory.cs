namespace Backend.Services.Grading;

internal sealed class GradingTestFileFactory
{
    public void Write(string directory, string solutionCode, string testCode, GradingLanguage language)
    {
        if (language == GradingLanguage.JavaScript || language == GradingLanguage.TypeScript)
        {
            WriteJestFiles(directory, solutionCode, testCode, language);
            return;
        }

        WritePythonFiles(directory, solutionCode, testCode);
    }

    private static void WritePythonFiles(string directory, string solutionCode, string testCode)
    {
        File.WriteAllText(Path.Combine(directory, "solution.py"), solutionCode);
        File.WriteAllText(Path.Combine(directory, "test_solution.py"), testCode);
    }

    private static void WriteJestFiles(
        string directory,
        string solutionCode,
        string testCode,
        GradingLanguage language)
    {
        if (language == GradingLanguage.TypeScript)
        {
            WriteTypeScriptFiles(directory, solutionCode, testCode);
            return;
        }

        WriteJavaScriptFiles(directory, solutionCode, testCode);
    }

    private static void WriteJavaScriptFiles(string directory, string solutionCode, string testCode)
    {
        File.WriteAllText(Path.Combine(directory, "solution.js"), $$"""
{{solutionCode}}

module.exports = { solve };
""");
        File.WriteAllText(Path.Combine(directory, "solution.test.js"), testCode);
    }

    private static void WriteTypeScriptFiles(string directory, string solutionCode, string testCode)
    {
        File.WriteAllText(Path.Combine(directory, "solution.ts"), $$"""
{{solutionCode}}

(globalThis as any).__ojsharpSolve = solve;
""");
        File.WriteAllText(Path.Combine(directory, "solution.test.js"), $$"""
require("./solution.js");

{{testCode}}
""");
    }
}
