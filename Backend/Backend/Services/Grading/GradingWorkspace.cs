namespace Backend.Services.Grading;

internal sealed class GradingWorkspace
{
    private const string ContainerRoot = "/workspace";
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

        return new GradingRunWorkspace(hostPath, $"{ContainerRoot}/{runName}");
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
        return OperatingSystem.IsWindows()
            ? Path.Combine(Path.GetTempPath(), "ojsharp-grader-workspace")
            : "/tmp/ojsharp-grader-workspace";
    }
}

internal sealed class GradingRunWorkspace : IDisposable
{
    public GradingRunWorkspace(string hostPath, string containerPath)
    {
        HostPath = hostPath;
        ContainerPath = containerPath;
    }

    public string HostPath { get; }

    public string ContainerPath { get; }

    public void Dispose()
    {
        if (Directory.Exists(HostPath))
        {
            Directory.Delete(HostPath, true);
        }
    }
}
