using System.Text.Json;
using Backend.Contracts;
using Backend.Api;
using Backend.Configuration;
using Backend.Persistence;
using Backend.Services;
using Backend.Services.Grading;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration
        .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args);
}

builder.WebHost.UseUrls(builder.Configuration["BackendUrls"] ?? "http://localhost:5140");

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
});

var connectionString = ResolveConnectionString(builder);

builder.Services.AddDbContext<OjSharpDbContext>(options => options.UseNpgsql(
    connectionString,
    npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(2),
        errorCodesToAdd: new[] { "40P01", "40001" })));
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
builder.Services.AddHttpClient();
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.Configure<LocalLlmOptions>(builder.Configuration.GetSection(LocalLlmOptions.SectionName));
builder.Services.Configure<DeepseekOptions>(builder.Configuration.GetSection(DeepseekOptions.SectionName));
builder.Services.AddSingleton<AiCompletionService>();
builder.Services.AddSingleton<AiAssistantService>();
builder.Services.AddSingleton<AssessmentDraftGenerationService>();
builder.Services.AddScoped<SeedAdminSeeder>();
builder.Services.AddScoped<SchemaCompatibilityService>();
builder.Services.Configure<SeedAdminOptions>(builder.Configuration.GetSection(SeedAdminOptions.SectionName));

var app = builder.Build();

app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception exception)
    {
        if (context.Response.HasStarted)
        {
            throw;
        }

        var postgresException = FindPostgresException(exception);
        var isTransientDatabaseConflict = postgresException?.SqlState is "40P01" or "40001";
        app.Logger.LogError(exception, "Unhandled API exception.");

        context.Response.Clear();
        context.Response.StatusCode = isTransientDatabaseConflict
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        var message = isTransientDatabaseConflict
            ? "A transient database conflict occurred. Please retry the request."
            : "An internal server error occurred.";
        await context.Response.WriteAsJsonAsync(
            ApiResponse<object>.Failure("INTERNAL_ERROR", message),
            cancellationToken: context.RequestAborted);
    }
});

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
        await using var initializationLock = await DatabaseAdvisoryLocks.AcquireSessionLockAsync(
            dbContext,
            DatabaseAdvisoryLocks.SchemaCompatibility,
            CancellationToken.None);

        await dbContext.Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<SchemaCompatibilityService>().EnsureAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<SeedAdminSeeder>().SeedAsync(CancellationToken.None);
    }
    catch (Exception exception)
    {
        Backend.StartupDiagnostics.LogDatabaseInitializationFailure(app.Logger, exception);
        throw;
    }
}

static string ResolveConnectionString(WebApplicationBuilder builder)
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        return connectionString;
    }

    throw new InvalidOperationException("ConnectionStrings__DefaultConnection must be configured.");
}

static PostgresException? FindPostgresException(Exception exception)
{
    Exception? current = exception;
    while (current is not null)
    {
        if (current is PostgresException postgresException)
        {
            return postgresException;
        }

        current = current.InnerException;
    }

    return null;
}

public partial class Program;
