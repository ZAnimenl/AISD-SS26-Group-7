using Backend.Services.Grading;

namespace OjSharp.Tests.ApiContractTests;

public sealed class DockerEndpointResolutionTests
{
    [Fact]
    public void Docker_endpoint_resolution_prefers_explicit_docker_host()
    {
        var endpoint = DockerGraderContainer.TryResolveDockerEndpoint(
            " unix:///custom/docker.sock ",
            "/home/student",
            isWindows: false,
            _ => false);

        Assert.Equal("unix:///custom/docker.sock", endpoint);
    }

    [Fact]
    public void Docker_endpoint_resolution_normalizes_windows_named_pipe_hosts()
    {
        var endpoint = DockerGraderContainer.TryResolveDockerEndpoint(
            "npipe:////./pipe/dockerDesktopLinuxEngine",
            "C:\\Users\\student",
            isWindows: true,
            _ => false);

        Assert.Equal("npipe://./pipe/dockerDesktopLinuxEngine", endpoint);
    }

    [Fact]
    public void Docker_endpoint_resolution_detects_docker_desktop_socket_under_home()
    {
        var home = "/home/student";
        var dockerDesktopSocket = Path.Combine(home, ".docker", "run", "docker.sock");

        var endpoint = DockerGraderContainer.TryResolveDockerEndpoint(
            null,
            home,
            isWindows: false,
            path => path == dockerDesktopSocket);

        Assert.Equal($"unix://{dockerDesktopSocket}", endpoint);
    }

    [Fact]
    public void Docker_endpoint_resolution_detects_colima_socket_under_home()
    {
        var home = "/home/student";
        var colimaSocket = Path.Combine(home, ".colima", "default", "docker.sock");

        var endpoint = DockerGraderContainer.TryResolveDockerEndpoint(
            null,
            home,
            isWindows: false,
            path => path == colimaSocket);

        Assert.Equal($"unix://{colimaSocket}", endpoint);
    }

    [Fact]
    public void Docker_endpoint_resolution_returns_null_without_unix_socket_candidate()
    {
        var endpoint = DockerGraderContainer.TryResolveDockerEndpoint(
            null,
            "/home/student",
            isWindows: false,
            _ => false);

        Assert.Null(endpoint);
    }

    [Fact]
    public void Docker_endpoint_resolution_keeps_existing_unix_default_for_runner_fallback()
    {
        var endpoint = DockerGraderContainer.ResolveDockerEndpoint(
            null,
            "/home/student",
            isWindows: false,
            _ => false);

        Assert.Equal("unix:///var/run/docker.sock", endpoint);
    }
}
