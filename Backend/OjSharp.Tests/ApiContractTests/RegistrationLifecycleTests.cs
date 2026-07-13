using Backend.Api;
using Backend.Configuration;
using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace OjSharp.Tests.ApiContractTests;

public sealed class RegistrationLifecycleTests
{
    [Fact]
    public async Task Restarting_abandoned_registration_with_same_identity_succeeds()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var pendingRegistrations = new PendingRegistrationStore(TimeProvider.System);
        var (emailService, logger) = CreateEmailDelivery();

        var first = AssertSuccessfulStart(await AuthEndpoints.RegisterStartAsync(
            new RegisterStartRequest("Ada Student", "ada-student", "ada@example.com"),
            dbContext,
            emailService,
            logger,
            pendingRegistrations,
            CancellationToken.None));

        var restarted = AssertSuccessfulStart(await AuthEndpoints.RegisterStartAsync(
            new RegisterStartRequest("Ada Student", "  ADA-STUDENT  ", "  ADA@example.com  "),
            dbContext,
            emailService,
            logger,
            pendingRegistrations,
            CancellationToken.None));

        Assert.Matches("^[0-9]{6}$", first.VerificationCode!);
        Assert.Matches("^[0-9]{6}$", restarted.VerificationCode!);
        Assert.Empty(await dbContext.Users.ToListAsync());
    }

    [Fact]
    public async Task Abandoned_pending_registration_does_not_reserve_username()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var pendingRegistrations = new PendingRegistrationStore(TimeProvider.System);
        var (emailService, logger) = CreateEmailDelivery();

        var first = AssertSuccessfulStart(await AuthEndpoints.RegisterStartAsync(
            new RegisterStartRequest("First Student", "shared-name", "first@example.com"),
            dbContext,
            emailService,
            logger,
            pendingRegistrations,
            CancellationToken.None));
        var second = AssertSuccessfulStart(await AuthEndpoints.RegisterStartAsync(
            new RegisterStartRequest("Second Student", "SHARED-NAME", "second@example.com"),
            dbContext,
            emailService,
            logger,
            pendingRegistrations,
            CancellationToken.None));

        AssertSuccessfulResult(await AuthEndpoints.RegisterVerifyCodeAsync(
            new RegisterVerifyCodeRequest("first@example.com", first.VerificationCode!),
            pendingRegistrations,
            CancellationToken.None));
        AssertSuccessfulResult(await AuthEndpoints.RegisterVerifyCodeAsync(
            new RegisterVerifyCodeRequest("second@example.com", second.VerificationCode!),
            pendingRegistrations,
            CancellationToken.None));
        Assert.Empty(await dbContext.Users.ToListAsync());
    }

    [Fact]
    public async Task First_completion_claims_username_for_other_pending_registrations()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var pendingRegistrations = new PendingRegistrationStore(TimeProvider.System);
        var (emailService, logger) = CreateEmailDelivery();
        var passwordHasher = new PasswordHasher();
        var tokenService = new AuthTokenService(Options.Create(new AuthOptions()));
        var completionCoordinator = new RegistrationCompletionCoordinator();

        var first = AssertSuccessfulStart(await AuthEndpoints.RegisterStartAsync(
            new RegisterStartRequest("First Student", "shared-name", "first-wins@example.com"),
            dbContext,
            emailService,
            logger,
            pendingRegistrations,
            CancellationToken.None));
        var second = AssertSuccessfulStart(await AuthEndpoints.RegisterStartAsync(
            new RegisterStartRequest("Second Student", "shared-name", "second-loses@example.com"),
            dbContext,
            emailService,
            logger,
            pendingRegistrations,
            CancellationToken.None));

        AssertSuccessfulResult(await AuthEndpoints.RegisterCompleteAsync(
            new RegisterCompleteRequest("first-wins@example.com", first.VerificationCode!, "password"),
            dbContext,
            passwordHasher,
            tokenService,
            pendingRegistrations,
            completionCoordinator,
            CancellationToken.None));

        var conflict = await AuthEndpoints.RegisterCompleteAsync(
            new RegisterCompleteRequest("second-loses@example.com", second.VerificationCode!, "password"),
            dbContext,
            passwordHasher,
            tokenService,
            pendingRegistrations,
            completionCoordinator,
            CancellationToken.None);

        AssertConflict(conflict, "USERNAME_TAKEN");
        var user = Assert.Single(await dbContext.Users.ToListAsync());
        Assert.Equal("first-wins@example.com", user.Email);
        Assert.Equal("shared-name", user.Username);
        Assert.True(user.EmailVerified);
    }

    [Fact]
    public async Task Persisted_username_remains_unavailable_at_registration_start()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            FullName = "Existing Student",
            Username = "owned-name",
            Email = "owner@example.com",
            PasswordHash = "not-used",
            Role = UserRoles.Student,
            Status = UserStatuses.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            AuthProvider = "email",
            EmailVerified = true
        });
        await dbContext.SaveChangesAsync();

        var pendingRegistrations = new PendingRegistrationStore(TimeProvider.System);
        var (emailService, logger) = CreateEmailDelivery();
        var result = await AuthEndpoints.RegisterStartAsync(
            new RegisterStartRequest("New Student", "OWNED-NAME", "new@example.com"),
            dbContext,
            emailService,
            logger,
            pendingRegistrations,
            CancellationToken.None);

        AssertConflict(result, "USERNAME_TAKEN");
        Assert.Single(await dbContext.Users.ToListAsync());
    }

    [Fact]
    public async Task Concurrent_completions_create_only_one_case_insensitive_username_owner()
    {
        var databaseName = $"registration-{Guid.NewGuid():N}";
        var connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
        await using var anchorConnection = await OpenConnectionAsync(connectionString);
        await using var setupContext = await CreateDbContextAsync(anchorConnection);
        var pendingRegistrations = new PendingRegistrationStore(TimeProvider.System);
        var completionCoordinator = new RegistrationCompletionCoordinator();
        var (emailService, logger) = CreateEmailDelivery();
        var passwordHasher = new PasswordHasher();
        var tokenService = new AuthTokenService(Options.Create(new AuthOptions()));

        var first = AssertSuccessfulStart(await AuthEndpoints.RegisterStartAsync(
            new RegisterStartRequest("First Student", "race-name", "race-first@example.com"),
            setupContext,
            emailService,
            logger,
            pendingRegistrations,
            CancellationToken.None));
        var second = AssertSuccessfulStart(await AuthEndpoints.RegisterStartAsync(
            new RegisterStartRequest("Second Student", "RACE-NAME", "race-second@example.com"),
            setupContext,
            emailService,
            logger,
            pendingRegistrations,
            CancellationToken.None));

        await using var firstConnection = await OpenConnectionAsync(connectionString);
        await using var secondConnection = await OpenConnectionAsync(connectionString);
        await using var firstContext = await CreateDbContextAsync(firstConnection);
        await using var secondContext = await CreateDbContextAsync(secondConnection);

        var results = await Task.WhenAll(
            AuthEndpoints.RegisterCompleteAsync(
                new RegisterCompleteRequest("race-first@example.com", first.VerificationCode!, "password"),
                firstContext,
                passwordHasher,
                tokenService,
                pendingRegistrations,
                completionCoordinator,
                CancellationToken.None),
            AuthEndpoints.RegisterCompleteAsync(
                new RegisterCompleteRequest("race-second@example.com", second.VerificationCode!, "password"),
                secondContext,
                passwordHasher,
                tokenService,
                pendingRegistrations,
                completionCoordinator,
                CancellationToken.None));

        var statusCodes = results
            .Select(result => Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode)
            .Order()
            .ToArray();
        Assert.Equal([StatusCodes.Status200OK, StatusCodes.Status409Conflict], statusCodes);
        AssertConflict(
            results.Single(result => ((IStatusCodeHttpResult)result).StatusCode == StatusCodes.Status409Conflict),
            "USERNAME_TAKEN");

        setupContext.ChangeTracker.Clear();
        var user = Assert.Single(await setupContext.Users.ToListAsync());
        Assert.Equal("race-name", user.Username, ignoreCase: true);
    }

    [Fact]
    public async Task Concurrent_invalid_codes_cannot_bypass_attempt_limit()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var pendingRegistrations = new PendingRegistrationStore(TimeProvider.System);
        var (emailService, logger) = CreateEmailDelivery();

        AssertSuccessfulStart(await AuthEndpoints.RegisterStartAsync(
            new RegisterStartRequest("Attempt Student", "attempt-student", "attempts@example.com"),
            dbContext,
            emailService,
            logger,
            pendingRegistrations,
            CancellationToken.None));

        var invalidResults = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            AuthEndpoints.RegisterVerifyCodeAsync(
                new RegisterVerifyCodeRequest("attempts@example.com", "wrong-code"),
                pendingRegistrations,
                CancellationToken.None)));

        Assert.All(invalidResults, result => AssertError(
            result,
            StatusCodes.Status400BadRequest,
            "INVALID_CODE"));

        var exhausted = await AuthEndpoints.RegisterVerifyCodeAsync(
            new RegisterVerifyCodeRequest("attempts@example.com", "wrong-code"),
            pendingRegistrations,
            CancellationToken.None);
        AssertError(exhausted, StatusCodes.Status400BadRequest, "TOO_MANY_ATTEMPTS");

        var removed = await AuthEndpoints.RegisterVerifyCodeAsync(
            new RegisterVerifyCodeRequest("attempts@example.com", "wrong-code"),
            pendingRegistrations,
            CancellationToken.None);
        AssertError(removed, StatusCodes.Status400BadRequest, "NO_PENDING_REGISTRATION");
    }

    [Fact]
    public async Task Completion_requests_cannot_bypass_attempt_limit()
    {
        await using var connection = await OpenConnectionAsync();
        await using var dbContext = await CreateDbContextAsync(connection);
        var pendingRegistrations = new PendingRegistrationStore(TimeProvider.System);
        var completionCoordinator = new RegistrationCompletionCoordinator();
        var (emailService, logger) = CreateEmailDelivery();
        var passwordHasher = new PasswordHasher();
        var tokenService = new AuthTokenService(Options.Create(new AuthOptions()));

        AssertSuccessfulStart(await AuthEndpoints.RegisterStartAsync(
            new RegisterStartRequest("Complete Student", "complete-student", "complete-attempts@example.com"),
            dbContext,
            emailService,
            logger,
            pendingRegistrations,
            CancellationToken.None));

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var invalid = await AuthEndpoints.RegisterCompleteAsync(
                new RegisterCompleteRequest("complete-attempts@example.com", "wrong-code", "password"),
                dbContext,
                passwordHasher,
                tokenService,
                pendingRegistrations,
                completionCoordinator,
                CancellationToken.None);
            AssertError(invalid, StatusCodes.Status400BadRequest, "INVALID_CODE");
        }

        var exhausted = await AuthEndpoints.RegisterCompleteAsync(
            new RegisterCompleteRequest("complete-attempts@example.com", "wrong-code", "password"),
            dbContext,
            passwordHasher,
            tokenService,
            pendingRegistrations,
            completionCoordinator,
            CancellationToken.None);

        AssertError(exhausted, StatusCodes.Status400BadRequest, "TOO_MANY_ATTEMPTS");
        Assert.Empty(await dbContext.Users.ToListAsync());
    }

    private static RegistrationCodeDeliveryResponse AssertSuccessfulStart(IResult result)
    {
        var response = Assert.IsType<Ok<ApiResponse<RegistrationCodeDeliveryResponse>>>(result);
        var envelope = Assert.IsType<ApiResponse<RegistrationCodeDeliveryResponse>>(response.Value);
        Assert.True(envelope.Ok);
        Assert.Null(envelope.Error);
        return Assert.IsType<RegistrationCodeDeliveryResponse>(envelope.Data);
    }

    private static void AssertSuccessfulResult(IResult result)
    {
        var response = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
    }

    private static void AssertConflict(IResult result, string expectedCode)
    {
        AssertError(result, StatusCodes.Status409Conflict, expectedCode);
    }

    private static void AssertError(IResult result, int expectedStatusCode, string expectedCode)
    {
        var response = Assert.IsType<JsonHttpResult<ApiResponse<object>>>(result);
        Assert.Equal(expectedStatusCode, response.StatusCode);
        var envelope = Assert.IsType<ApiResponse<object>>(response.Value);
        Assert.False(envelope.Ok);
        Assert.Null(envelope.Data);
        Assert.Equal(expectedCode, envelope.Error?.Code);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync(
        string connectionString = "Data Source=:memory:")
    {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<OjSharpDbContext> CreateDbContextAsync(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<OjSharpDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new OjSharpDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private static (EmailService Service, ILogger<EmailService> Logger) CreateEmailDelivery()
    {
        var logger = NullLogger<EmailService>.Instance;
        return (new EmailService(Options.Create(new SmtpOptions()), logger), logger);
    }
}
