using System.Text.Json;
using Backend.Domain;
using Backend.Services;
using Backend.Services.Grading;
using Docker.DotNet;

namespace OjSharp.Tests.ApiContractTests;

public sealed class GeneratedBrowserDependencyIntegrationTests
{
    [Fact]
    public async Task Generated_browser_check_dependencies_work_without_student_installation()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        var graderContainer = new DockerGraderContainer();
        using var readinessTimeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await graderContainer.EnsureReadyAsync(readinessTimeout.Token);
        var runner = new DockerCodeRunner(
            graderContainer,
            new GradingWorkspace(),
            new GradingTestFileFactory(),
            new GraderCommandFactory());
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            QuestionId = Guid.NewGuid(),
            Name = "Generated browser dependencies",
            Visibility = TestCaseVisibilities.Public,
            TestCodeJson = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["html"] = """
                    const indexedDBModule = require('fake-indexeddb');
                    const fetchMock = require('jest-fetch-mock');
                    global.indexedDB = indexedDBModule;
                    global.fetch = fetchMock;

                    test('generated browser dependencies work', async () => {
                      const dom = new JSDOM('<button>Clear All</button>', { url: 'http://localhost' });
                      expect(dom.window.document.querySelector('button').textContent).toBe('Clear All');
                      expect(typeof indexedDB.open).toBe('function');
                      await new Promise((resolve, reject) => {
                        const request = indexedDB.open('todo-offline-queue', 1);
                        request.onupgradeneeded = () => request.result.createObjectStore('todos', { keyPath: 'id' });
                        request.onerror = () => reject(request.error);
                        request.onsuccess = () => {
                          const transaction = request.result.transaction('todos', 'readwrite');
                          transaction.objectStore('todos').put({ id: '1', title: 'Offline Todo' });
                          transaction.oncomplete = resolve;
                          transaction.onerror = () => reject(transaction.error);
                        };
                      });
                      fetchMock.mockResponseOnce(JSON.stringify({ ok: true }));
                      await expect(fetch('/todos').then(response => response.json())).resolves.toEqual({ ok: true });
                    });
                    """
            })
        };

        var result = await runner.RunAsync(
            new Dictionary<string, string>
            {
                ["index.html"] = "<!doctype html><html><body><main id=\"app\"></main><script src=\"app.js\"></script></body></html>",
                ["app.js"] = "document.getElementById('app').textContent = 'Todo';"
            },
            "html",
            testCase,
            CancellationToken.None);

        Assert.True(result.ExitCode == 0, result.Stderr ?? result.Stdout);
        Assert.False(result.TimedOut);
        Assert.Null(result.Stderr);
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
