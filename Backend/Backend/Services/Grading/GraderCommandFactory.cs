namespace Backend.Services.Grading;

internal sealed class GraderCommandFactory
{
    public string[] Create(GradingLanguage language)
    {
        return language switch
        {
            GradingLanguage.Python =>
                ["timeout", "3s", "pytest", "-q", "test_solution.py", "--tb=short", "--disable-warnings", "-p", "no:cacheprovider"],
            GradingLanguage.TypeScript =>
                ["timeout", "3s", "sh", "-c", "tsc solution.ts --target ES2020 --module commonjs --esModuleInterop --skipLibCheck && jest --runInBand solution.test.js --silent=false --no-cache"],
            _ =>
                ["timeout", "3s", "jest", "--runInBand", "solution.test.js", "--silent=false", "--no-cache"]
        };
    }
}
