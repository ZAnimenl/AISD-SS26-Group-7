using System.Text.Json;
using Backend.Contracts;
using Backend.Api;
using Backend.Configuration;
using Backend.Persistence;
using Backend.Services;
using Backend.Services.Grading;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Data.Sqlite;
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
var databaseProvider = ResolveDatabaseProvider(builder, connectionString);

builder.Services.AddDbContext<OjSharpDbContext>(options =>
{
    if (DatabaseProviders.IsSqlite(databaseProvider))
    {
        options.UseSqlite(connectionString);
        return;
    }

    options.UseNpgsql(
        connectionString,
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(2),
            errorCodesToAdd: new[] { "40P01", "40001" }));
});
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
builder.Services.AddSingleton<DockerRuntimeProbe>();
builder.Services.AddSingleton<DockerGraderContainer>();
builder.Services.AddSingleton<GradingWorkspace>();
builder.Services.AddSingleton<GradingTestFileFactory>();
builder.Services.AddSingleton<GraderCommandFactory>();
builder.Services.AddSingleton<ICodeRunner, DockerCodeRunner>();
builder.Services.AddSingleton<CodeEvaluationService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.Configure<LocalLlmOptions>(builder.Configuration.GetSection(LocalLlmOptions.SectionName));
builder.Services.Configure<DeepseekOptions>(builder.Configuration.GetSection(DeepseekOptions.SectionName));
builder.Services.AddSingleton<AiCompletionService>();
builder.Services.AddSingleton<AiAssistantService>();
builder.Services.AddScoped<AiUsageGradingService>();
builder.Services.AddHostedService<ReflectionDeadlineWorker>();
builder.Services.AddHostedService<AssessmentExpirationWorker>();
builder.Services.AddSingleton<CanonicalPrototypeSource>();
builder.Services.AddSingleton<AssessmentDraftGenerationService>();
builder.Services.AddSingleton<TokenEfficiencyReferenceBaselineService>();
builder.Services.AddHostedService<GraderWarmupService>();
builder.Services.AddScoped<SeedAdminSeeder>();
builder.Services.AddScoped<SchemaCompatibilityService>();
builder.Services.AddScoped<SqliteAuthSchemaMigrator>();
builder.Services.Configure<SeedAdminOptions>(builder.Configuration.GetSection(SeedAdminOptions.SectionName));

// New auth additions
builder.Services.Configure<GoogleOAuthOptions>(builder.Configuration.GetSection(GoogleOAuthOptions.SectionName));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddSingleton<GoogleOAuthService>();
builder.Services.AddSingleton<EmailService>();

var app = builder.Build();

if (args.Contains("--seed-admin-only", StringComparer.OrdinalIgnoreCase))
{
    await SeedDatabaseAsync(app);
    return;
}

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

        var isTransientDatabaseConflict = IsTransientDatabaseConflict(exception);
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
ReflectionEndpoints.Map(api);
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
        // SQLite-only patch: add new auth columns to existing local databases so teammates
        // who already had a DB file from before the auth refactor do not need to delete it.
        await scope.ServiceProvider.GetRequiredService<SqliteAuthSchemaMigrator>().EnsureAsync(CancellationToken.None);
        await scope.ServiceProvider.GetRequiredService<SeedAdminSeeder>().SeedAsync(CancellationToken.None);
    }
    catch (Exception exception)
    {
        Backend.StartupDiagnostics.LogDatabaseInitializationFailure(app.Logger, exception);
        throw;
    }
}

static string ResolveDatabaseProvider(WebApplicationBuilder builder, string connectionString)
{
    var configuredProvider = builder.Configuration["Database:Provider"]
        ?? builder.Configuration["Database__Provider"];

    if (!string.IsNullOrWhiteSpace(configuredProvider))
    {
        return configuredProvider;
    }

    return connectionString.TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
        ? DatabaseProviders.Sqlite
        : DatabaseProviders.PostgreSql;
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

static bool IsTransientDatabaseConflict(Exception exception)
{
    Exception? current = exception;
    while (current is not null)
    {
        if (current is PostgresException postgresException)
        {
            return postgresException.SqlState is "40P01" or "40001";
        }

        if (current is SqliteException sqliteException)
        {
            return sqliteException.SqliteErrorCode is 5 or 6;
        }

        current = current.InnerException;
    }

    return false;
}

public partial class Program;
