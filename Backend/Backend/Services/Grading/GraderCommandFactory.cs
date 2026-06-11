namespace Backend.Services.Grading;

internal sealed class GraderCommandFactory
{
    private const string ExecutionTimeout = "8s";

    public string[] Create(GradingLanguage language)
    {
        return language switch
        {
            GradingLanguage.Python =>
                ["timeout", ExecutionTimeout, "pytest", "-q", "test_solution.py", "--tb=short", "--disable-warnings", "-p", "no:cacheprovider"],
            GradingLanguage.TypeScript =>
                ["timeout", ExecutionTimeout, "sh", "-c", "tsc solution.ts --target ES2020 --module commonjs --esModuleInterop --skipLibCheck && jest --env=jsdom --config={} --setupFiles ./jest.setup.js --runInBand solution.test.js --silent=false --no-cache"],
            _ =>
                ["timeout", ExecutionTimeout, "jest", "--env=jsdom", "--config={}", "--setupFiles", "./jest.setup.js", "--runInBand", "solution.test.js", "--silent=false", "--no-cache"]
        };
    }
}
