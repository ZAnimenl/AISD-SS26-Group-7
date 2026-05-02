using System.Text.Json;

namespace OjSharp.Tests.ApiContractTests;

public sealed class BackendConfigurationTests
{
    private const string ProductionConnectionString = "Host=localhost:5432;Database=ai_coding;Username=ai_coding;password=password";
    private const string DevelopmentConnectionString = "Host=localhost:5433;Database=ai_coding;Username=ai_coding;password=password";

    [Fact]
    public void Appsettings_uses_production_postgres_port()
    {
        using var document = ReadAppsettings("appsettings.json");

        var connectionString = document.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString();

        Assert.Equal(ProductionConnectionString, connectionString);
    }

    [Fact]
    public void Development_appsettings_uses_debug_postgres_port()
    {
        using var document = ReadAppsettings("appsettings.Development.json");

        var connectionString = document.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("DefaultConnection")
            .GetString();

        Assert.Equal(DevelopmentConnectionString, connectionString);
    }

    [Fact]
    public void Appsettings_does_not_store_production_seed_admin_credentials()
    {
        using var document = ReadAppsettings("appsettings.json");

        Assert.False(document.RootElement.TryGetProperty("SeedAdmin", out _));
    }

    [Fact]
    public void Development_appsettings_configures_local_seed_admin_demo_login()
    {
        using var document = ReadAppsettings("appsettings.Development.json");

        var seedAdmin = document.RootElement.GetProperty("SeedAdmin");

        Assert.Equal("admin@example.com", seedAdmin.GetProperty("Email").GetString());
        Assert.Equal("password", seedAdmin.GetProperty("Password").GetString());
    }

    private static JsonDocument ReadAppsettings(string fileName)
    {
        var solutionDirectory = FindSolutionDirectory();
        var path = Path.Combine(solutionDirectory.FullName, "Backend", fileName);

        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static DirectoryInfo FindSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Backend.sln")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new DirectoryNotFoundException("Backend solution directory was not found.");
    }
}
