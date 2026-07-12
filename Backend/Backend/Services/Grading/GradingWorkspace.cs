namespace Backend.Services.Grading;

internal sealed class GradingWorkspace
{
    private readonly string hostRoot;

    public GradingWorkspace()
        : this(GetDefaultHostRoot())
    {
    }

    public GradingWorkspace(string hostRoot)
    {
        this.hostRoot = hostRoot;
    }

    public string HostRoot => hostRoot;

    public GradingRunWorkspace CreateRun()
    {
        Directory.CreateDirectory(hostRoot);
        var runName = Guid.NewGuid().ToString("N");
        var hostPath = Path.Combine(hostRoot, runName);
        Directory.CreateDirectory(hostPath);

        return new GradingRunWorkspace(hostPath);
    }

    public string? ReadActualOutput(GradingRunWorkspace run)
    {
        var actualPath = Path.Combine(run.HostPath, "actual.txt");
        return File.Exists(actualPath)
            ? File.ReadAllText(actualPath)
            : null;
    }

    private static string GetDefaultHostRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Path.GetTempPath(), "ojsharp-grader-workspace");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(home)
            ? Path.Combine(Path.GetTempPath(), "ojsharp-grader-workspace")
            : Path.Combine(home, ".ojsharp", "grader-workspace");
    }
}

internal sealed class GradingRunWorkspace : IDisposable
{
    public GradingRunWorkspace(string hostPath)
    {
        HostPath = hostPath;
    }

    public string HostPath { get; }

    public void Dispose()
    {
        if (Directory.Exists(HostPath))
        {
            Directory.Delete(HostPath, true);
        }
    }
}
