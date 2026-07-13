namespace Backend.Services.Grading;

internal sealed class GraderCommandFactory
{
    private const string ExecutionTimeout = "9s";
    private const string BrowserPreviewTimeout = "3s";

    public string[] Create(GradingLanguage language)
    {
        return language switch
        {
            GradingLanguage.Python =>
                ["timeout", ExecutionTimeout, "sh", "-c", "TODO_DATABASE_PATH=\"$PWD/todos.db\" pytest -q test_solution.py --tb=short --disable-warnings -p no:cacheprovider"],
            GradingLanguage.TypeScript =>
                ["timeout", ExecutionTimeout, "sh", "-c", "tsc solution.ts --target ES2020 --module commonjs --esModuleInterop --skipLibCheck && jest --env=jsdom --config={} --setupFiles ./jest.setup.js --runInBand --runTestsByPath solution.test.js --silent=false --no-cache"],
            GradingLanguage.Sql =>
                ["timeout", ExecutionTimeout, "jest", "--env=jsdom", "--config={}", "--setupFiles", "./jest.setup.js", "--runInBand", "--runTestsByPath", "solution.test.js", "--silent=false", "--no-cache"],
            _ =>
                ["timeout", ExecutionTimeout, "jest", "--env=jsdom", "--config={}", "--setupFiles", "./jest.setup.js", "--runInBand", "--runTestsByPath", "solution.test.js", "--silent=false", "--no-cache"]
        };
    }

    public string[] CreateBrowserPreview(string previewEntry)
    {
        return ["timeout", BrowserPreviewTimeout, "node", "browser-preview.js", previewEntry];
    }
}
