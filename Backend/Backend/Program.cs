using System.Text.Json;
using Backend.Api;
using Backend.Configuration;
using Backend.Persistence;
using Backend.Services;
using Backend.Services.Grading;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(builder.Configuration["BackendUrls"] ?? "http://localhost:5040");

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Host=localhost:5433;Database=ai_coding;Username=ai_coding;password=password";

builder.Services.AddDbContext<OjSharpDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});

builder.Services.AddSingleton<AuthTokenService>();
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<SessionClock>();
builder.Services.AddSingleton<AssessmentProjectionService>();
builder.Services.AddSingleton<WorkspaceProjectionService>();
builder.Services.AddSingleton<ICodeRunner, DockerCodeRunner>();
builder.Services.AddSingleton<CodeEvaluationService>();
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddSingleton<AiMockService>();
builder.Services.AddScoped<DemoDataSeeder>();
builder.Services.AddScoped<SchemaCompatibilityService>();
builder.Services.Configure<SeedAdminOptions>(builder.Configuration.GetSection(SeedAdminOptions.SectionName));

var app = builder.Build();

app.UseCors();

await SeedDatabaseAsync(app);

app.MapGet("/", () => Results.Redirect("/api/v1/health"));

var api = app.MapGroup("/api/v1");
SystemEndpoints.Map(api);
AuthEndpoints.Map(api);
UserManagementEndpoints.Map(api);
StudentEndpoints.Map(api);
AdminDashboardEndpoints.Map(api);
AssessmentEndpoints.Map(api);
QuestionEndpoints.Map(api);
SessionEndpoints.Map(api);
WorkspaceEndpoints.Map(api);
ExecutionEndpoints.Map(api);
SubmissionEndpoints.Map(api);
AiEndpoints.Map(api);
ReportEndpoints.Map(api);

app.Run();

static async Task SeedDatabaseAsync(WebApplication app)
{
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OjSharpDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<SchemaCompatibilityService>().EnsureAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync(CancellationToken.None);
    }
    catch (Exception exception)
    {
        Backend.StartupDiagnostics.LogDatabaseInitializationFailure(app.Logger, exception);
    }
}

public partial class Program;
