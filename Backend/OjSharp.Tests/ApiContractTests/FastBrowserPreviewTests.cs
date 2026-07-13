using System.Diagnostics;
using System.Text.Json;
using Backend.Api;
using Backend.Domain;
using Backend.Services;
using Backend.Services.Grading;
using Docker.DotNet;

namespace OjSharp.Tests.ApiContractTests;

public sealed class FastBrowserPreviewTests
{
    [Fact]
    public void Synthetic_html_preview_selects_the_isolated_packager_profile()
    {
        var testCase = CreateBrowserPreviewTest();
        var publicMetadata = JsonDocumentSerializer.Deserialize(
            testCase.PublicMetadataJson,
            new Dictionary<string, string>());
        var adminMetadata = JsonDocumentSerializer.Deserialize(
            testCase.AdminMetadataJson,
            new Dictionary<string, string>());

        Assert.Equal("index.html", publicMetadata["preview_entry"]);
        Assert.Equal("browser_preview_packager", adminMetadata["execution_profile"]);
        Assert.Equal("browser_ui_preview_run", adminMetadata["source"]);
    }

    [Fact]
    public void Browser_preview_workspace_rejects_nested_submitted_paths()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ojsharp-preview-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var factory = new GradingTestFileFactory();

            var exception = Assert.Throws<InvalidOperationException>(() => factory.WriteBrowserPreview(
                directory,
                new Dictionary<string, string> { ["../outside.html"] = "<main>Unsafe</main>" }));

            Assert.Contains("safe workspace basenames", exception.Message);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Warm_browser_preview_inlines_local_assets_within_five_seconds()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var files = new Dictionary<string, string>
        {
            ["index.html"] = "<!doctype html><html><head><link rel=\"stylesheet\" href=\"styles.css\"></head><body><main>Todo</main><script src=\"app.js\"></script></body></html>",
            ["styles.css"] = "main { color: navy; }",
            ["app.js"] = "document.querySelector('main').dataset.ready = 'true';"
        };
        var runner = new DockerCodeRunner();
        await runner.RunAsync(files, "html", CreateBrowserPreviewTest(), CancellationToken.None);
        var stopwatch = Stopwatch.StartNew();

        var result = await runner.RunAsync(
            files,
            "html",
            CreateBrowserPreviewTest(),
            CancellationToken.None);

        stopwatch.Stop();
        Assert.True(result.ExitCode == 0, result.Stderr ?? result.Stdout);
        Assert.False(result.TimedOut);
        Assert.Contains("data-sandbox-inline=\"styles.css\"", result.Stdout);
        Assert.Contains("data-sandbox-inline=\"app.js\"", result.Stdout);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Warm browser preview took {stopwatch.Elapsed.TotalSeconds:F3} seconds.");
    }

    private static TestCase CreateBrowserPreviewTest()
    {
        var question = new Question
        {
            Id = Guid.NewGuid(),
            TaskType = TaskTypes.FrontendUiExtension,
            VerificationMode = VerificationModes.BrowserUiPreview,
            StarterCodeJson = JsonSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
            {
                ["html"] = new()
                {
                    ["index.html"] = "<!doctype html><main>Todo</main>",
                    ["styles.css"] = string.Empty,
                    ["app.js"] = string.Empty
                }
            }),
            VerificationMetadataJson = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["preview_entry"] = "index.html"
            })
        };
        var method = typeof(ExecutionEndpoints).GetMethod(
            "CreateBrowserPreviewTest",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<TestCase>(method!.Invoke(null, [question, "html"]));
    }

    private static bool IsDockerAvailable()
    {
        try
        {
            var endpoint = DockerGraderContainer.ResolveDockerEndpoint(
                Environment.GetEnvironmentVariable("DOCKER_HOST"),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                OperatingSystem.IsWindows(),
                Path.Exists);
            using var client = new DockerClientConfiguration(new Uri(endpoint)).CreateClient();
            var ping = client.System.PingAsync();
            ping.Wait(1500);
            return ping.IsCompletedSuccessfully;
        }
        catch
        {
            return false;
        }
    }
}
