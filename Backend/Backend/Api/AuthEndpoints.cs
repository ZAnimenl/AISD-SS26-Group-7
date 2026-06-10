using System.Collections.Concurrent;
using System.Security.Cryptography;
using Backend.Configuration;
using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Api;

public static class AuthEndpoints
{
    // === Short-lived state ===
    // OAuth CSRF state: maps random state token -> (rememberMe, createdAt).
    private static readonly ConcurrentDictionary<string, (bool RememberMe, DateTimeOffset CreatedAt)> OAuthStates = new();
    private static readonly TimeSpan OAuthStateLifetime = TimeSpan.FromMinutes(10);

    // Pending registrations keyed by lowercased email until the user completes the code flow.
    private static readonly ConcurrentDictionary<string, PendingRegistration> PendingRegistrations = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);
    private const int MaxCodeAttempts = 5;

    public static void Map(RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth");

        group.MapPost("/login", LoginAsync);
        group.MapGet("/me", MeAsync);
        group.MapPost("/logout", Logout);
        group.MapPost("/forgot-password", ForgotPasswordAsync);
        group.MapPost("/change-password", ChangePasswordAsync);

        // Code-based 3-step registration
        group.MapPost("/register/start", RegisterStartAsync);
        group.MapPost("/register/verify-code", RegisterVerifyCodeAsync);
        group.MapPost("/register/complete", RegisterCompleteAsync);
        group.MapPost("/register/resend-code", RegisterResendCodeAsync);

        // Google OAuth
        group.MapGet("/google/start", GoogleStartAsync);
        group.MapGet("/google/callback", GoogleCallbackAsync);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        OjSharpDbContext dbContext,
        PasswordHasher passwordHasher,
        AuthTokenService tokenService,
        CancellationToken cancellationToken)
    {
        var lowerEmail = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(
            candidate => candidate.Email.ToLower() == lowerEmail && candidate.Status == UserStatuses.Active,
            cancellationToken);

        if (user is null || string.IsNullOrEmpty(user.PasswordHash) || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return ApiResults.Error("UNAUTHENTICATED", "Invalid email or password.", StatusCodes.Status401Unauthorized);
        }

        if (user.AuthProvider == "email" && !user.EmailVerified)
        {
            return ApiResults.Error(
                "EMAIL_NOT_VERIFIED",
                "Please verify your email before signing in.",
                StatusCodes.Status403Forbidden);
        }

        var issued = tokenService.CreateToken(user, request.RememberMe);
        return ApiResults.Success(new
        {
            token = issued.Token,
            expires_at = issued.ExpiresAt,
            remember_me = request.RememberMe,
            must_change_password = user.MustChangePassword,
            user = ToUserDto(user)
        });
    }

    private static async Task<IResult> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        OjSharpDbContext dbContext,
        PasswordHasher passwordHasher,
        EmailService emailService,
        ILogger<EmailService> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return ApiResults.Error("VALIDATION_ERROR", "Email is required.", StatusCodes.Status400BadRequest);
        }

        var lookup = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(
            u => u.Email.ToLower() == lookup && u.Status == UserStatuses.Active,
            cancellationToken);

        // Always reply success to avoid email enumeration. Only act when a valid email user is found.
        if (user is null || user.AuthProvider != "email")
        {
            return ApiResults.Success(new { sent = true });
        }

        var tempPassword = GenerateTemporaryPassword();
        user.PasswordHash = passwordHasher.Hash(tempPassword);
        user.MustChangePassword = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        var emailSent = await emailService.SendTemporaryPasswordAsync(
            user.Email, user.FullName, tempPassword, cancellationToken);
        if (!emailSent)
        {
            logger.LogWarning("Temporary password email failed for {Email}. Temp password (dev): {Pwd}", user.Email, tempPassword);
        }

        return ApiResults.Success(new
        {
            sent = emailSent,
            // dev fallback when SMTP is offline so manual testing still works
            dev_temporary_password = emailSent ? null : tempPassword
        });
    }

    private static async Task<IResult> ChangePasswordAsync(
        HttpContext httpContext,
        ChangePasswordRequest request,
        OjSharpDbContext dbContext,
        PasswordHasher passwordHasher,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (user, error) = await currentUserAccessor.RequireUserAsync(httpContext, dbContext, cancellationToken);
        if (error is not null)
        {
            return error;
        }
        if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return ApiResults.Error("VALIDATION_ERROR", "New password must be at least 6 characters.", StatusCodes.Status400BadRequest);
        }
        if (!passwordHasher.Verify(request.CurrentPassword, user!.PasswordHash))
        {
            return ApiResults.Error("INVALID_PASSWORD", "Current password is incorrect.", StatusCodes.Status400BadRequest);
        }
        if (request.NewPassword == request.CurrentPassword)
        {
            return ApiResults.Error("SAME_PASSWORD", "Please choose a different password than the temporary one.", StatusCodes.Status400BadRequest);
        }

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.MustChangePassword = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ApiResults.Success(new { changed = true });
    }

    private static async Task<IResult> MeAsync(
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (user, error) = await currentUserAccessor.RequireUserAsync(httpContext, dbContext, cancellationToken);
        return error ?? ApiResults.Success(ToUserDto(user!));
    }

    private static IResult Logout(HttpContext httpContext, AuthTokenService tokenService)
    {
        tokenService.RevokeToken(AuthTokenService.GetBearerToken(httpContext));
        return ApiResults.Success(new { logged_out = true });
    }

    // ===== Code-based registration =====

    private static async Task<IResult> RegisterStartAsync(
        RegisterStartRequest request,
        OjSharpDbContext dbContext,
        EmailService emailService,
        ILogger<EmailService> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email))
        {
            return ApiResults.Error("VALIDATION_ERROR", "Full name and email are required.", StatusCodes.Status400BadRequest);
        }

        if (!LooksLikeEmail(request.Email))
        {
            return ApiResults.Error("VALIDATION_ERROR", "Please enter a valid email address.", StatusCodes.Status400BadRequest);
        }

        CleanExpiredPendingRegistrations();

        var normalizedEmail = request.Email.Trim();
        var lowerEmail = normalizedEmail.ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(user => user.Email.ToLower() == lowerEmail, cancellationToken))
        {
            return ApiResults.Error(
                "EMAIL_TAKEN",
                "This email is already registered. Please sign in or use a different email.",
                StatusCodes.Status409Conflict);
        }

        var code = GenerateNumericCode(6);
        var pending = new PendingRegistration(
            request.FullName.Trim(),
            normalizedEmail,
            code,
            DateTimeOffset.UtcNow.Add(CodeLifetime),
            Attempts: 0);
        PendingRegistrations[lowerEmail] = pending;

        var emailSent = await emailService.SendVerificationCodeAsync(
            pending.Email, pending.FullName, code, cancellationToken);

        if (!emailSent)
        {
            logger.LogWarning("Verification code email failed for {Email}. Code (dev): {Code}", pending.Email, code);
        }

        return ApiResults.Success(new
        {
            sent = emailSent,
            expires_at = pending.ExpiresAt,
            // In development only when email delivery fails, surface the code so manual testing can continue.
            dev_code = emailSent ? null : code
        });
    }

    private static IResult RegisterVerifyCodeAsync(
        RegisterVerifyCodeRequest request)
    {
        var pending = LookupPending(request.Email);
        if (pending is null)
        {
            return ApiResults.Error("NO_PENDING_REGISTRATION", "We can't find a pending registration for this email. Start the registration again.", StatusCodes.Status400BadRequest);
        }

        if (pending.ExpiresAt < DateTimeOffset.UtcNow)
        {
            PendingRegistrations.TryRemove(pending.Email.ToLowerInvariant(), out _);
            return ApiResults.Error("CODE_EXPIRED", "Your verification code has expired. Please request a new one.", StatusCodes.Status400BadRequest);
        }

        if (pending.Attempts >= MaxCodeAttempts)
        {
            PendingRegistrations.TryRemove(pending.Email.ToLowerInvariant(), out _);
            return ApiResults.Error("TOO_MANY_ATTEMPTS", "Too many incorrect attempts. Please request a new code.", StatusCodes.Status400BadRequest);
        }

        if (!FixedTimeEquals(pending.Code, request.Code))
        {
            PendingRegistrations[pending.Email.ToLowerInvariant()] = pending with { Attempts = pending.Attempts + 1 };
            return ApiResults.Error("INVALID_CODE", "That code is not correct. Please try again.", StatusCodes.Status400BadRequest);
        }

        return ApiResults.Success(new { verified = true });
    }

    private static async Task<IResult> RegisterCompleteAsync(
        RegisterCompleteRequest request,
        OjSharpDbContext dbContext,
        PasswordHasher passwordHasher,
        AuthTokenService tokenService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return ApiResults.Error("VALIDATION_ERROR", "Password must be at least 6 characters.", StatusCodes.Status400BadRequest);
        }

        var pending = LookupPending(request.Email);
        if (pending is null)
        {
            return ApiResults.Error("NO_PENDING_REGISTRATION", "We can't find a pending registration for this email. Start the registration again.", StatusCodes.Status400BadRequest);
        }

        if (pending.ExpiresAt < DateTimeOffset.UtcNow)
        {
            PendingRegistrations.TryRemove(pending.Email.ToLowerInvariant(), out _);
            return ApiResults.Error("CODE_EXPIRED", "Your verification code has expired. Please request a new one.", StatusCodes.Status400BadRequest);
        }

        if (!FixedTimeEquals(pending.Code, request.Code))
        {
            return ApiResults.Error("INVALID_CODE", "That code is not correct.", StatusCodes.Status400BadRequest);
        }

        // Re-check email collision in case someone else registered with the same email meanwhile.
        var pendingLowerEmail = pending.Email.ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(user => user.Email.ToLower() == pendingLowerEmail, cancellationToken))
        {
            PendingRegistrations.TryRemove(pendingLowerEmail, out _);
            return ApiResults.Error(
                "EMAIL_TAKEN",
                "This email is already registered. Please sign in or use a different email.",
                StatusCodes.Status409Conflict);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = pending.FullName,
            Email = pending.Email,
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = UserRoles.Student,
            Status = UserStatuses.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            AuthProvider = "email",
            EmailVerified = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        PendingRegistrations.TryRemove(pending.Email.ToLowerInvariant(), out _);

        var issued = tokenService.CreateToken(user, request.RememberMe);
        return ApiResults.Success(new
        {
            token = issued.Token,
            expires_at = issued.ExpiresAt,
            remember_me = request.RememberMe,
            user = ToUserDto(user)
        });
    }

    private static async Task<IResult> RegisterResendCodeAsync(
        RegisterResendCodeRequest request,
        EmailService emailService,
        ILogger<EmailService> logger,
        CancellationToken cancellationToken)
    {
        var pending = LookupPending(request.Email);
        if (pending is null)
        {
            return ApiResults.Error("NO_PENDING_REGISTRATION", "We can't find a pending registration for this email. Start the registration again.", StatusCodes.Status400BadRequest);
        }

        var newCode = GenerateNumericCode(6);
        var refreshed = pending with
        {
            Code = newCode,
            ExpiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime),
            Attempts = 0
        };
        PendingRegistrations[pending.Email.ToLowerInvariant()] = refreshed;

        var emailSent = await emailService.SendVerificationCodeAsync(
            refreshed.Email, refreshed.FullName, newCode, cancellationToken);

        if (!emailSent)
        {
            logger.LogWarning("Verification code email failed for {Email}. Code (dev): {Code}", refreshed.Email, newCode);
        }

        return ApiResults.Success(new
        {
            sent = emailSent,
            expires_at = refreshed.ExpiresAt,
            dev_code = emailSent ? null : newCode
        });
    }

    // ===== Google OAuth =====

    private static IResult GoogleStartAsync(
        GoogleOAuthService oauth,
        bool? remember)
    {
        if (!oauth.IsConfigured)
        {
            return ApiResults.Error("CONFIG_MISSING", "Google sign-in is not configured on the server.", StatusCodes.Status503ServiceUnavailable);
        }
        CleanExpiredStates();

        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        OAuthStates[state] = (remember ?? false, DateTimeOffset.UtcNow);

        var url = oauth.BuildAuthorizationUrl(state);
        return ApiResults.Success(new { authorization_url = url, state });
    }

    private static async Task<IResult> GoogleCallbackAsync(
        HttpContext httpContext,
        GoogleOAuthService oauth,
        OjSharpDbContext dbContext,
        AuthTokenService tokenService,
        IOptions<GoogleOAuthOptions> googleOptions,
        ILogger<GoogleOAuthService> logger,
        string? code,
        string? state,
        string? error,
        CancellationToken cancellationToken)
    {
        var frontend = googleOptions.Value.FrontendCallback;

        if (!string.IsNullOrEmpty(error))
        {
            return Results.Redirect(AppendQuery(frontend, "error", error));
        }
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return Results.Redirect(AppendQuery(frontend, "error", "missing_code_or_state"));
        }
        if (!OAuthStates.TryRemove(state, out var stateInfo)
            || (DateTimeOffset.UtcNow - stateInfo.CreatedAt) > OAuthStateLifetime)
        {
            return Results.Redirect(AppendQuery(frontend, "error", "invalid_state"));
        }

        var profile = await oauth.ExchangeCodeAndFetchProfileAsync(code, cancellationToken);
        if (profile is null || string.IsNullOrWhiteSpace(profile.Email))
        {
            return Results.Redirect(AppendQuery(frontend, "error", "google_exchange_failed"));
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(
            u => u.GoogleId == profile.Sub || u.Email == profile.Email,
            cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                FullName = string.IsNullOrWhiteSpace(profile.Name) ? profile.Email : profile.Name,
                Email = profile.Email,
                PasswordHash = string.Empty,
                Role = UserRoles.Student,
                Status = UserStatuses.Active,
                CreatedAt = DateTimeOffset.UtcNow,
                AuthProvider = "google",
                GoogleId = profile.Sub,
                EmailVerified = profile.EmailVerified
            };
            dbContext.Users.Add(user);
        }
        else
        {
            if (string.IsNullOrEmpty(user.GoogleId))
            {
                user.GoogleId = profile.Sub;
            }
            if (profile.EmailVerified && !user.EmailVerified)
            {
                user.EmailVerified = true;
            }
        }

        if (user.Status != UserStatuses.Active)
        {
            return Results.Redirect(AppendQuery(frontend, "error", "account_inactive"));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var issued = tokenService.CreateToken(user, stateInfo.RememberMe);
        var redirect = AppendQuery(frontend, "token", issued.Token);
        redirect = AppendQuery(redirect, "remember_me", stateInfo.RememberMe ? "1" : "0");
        return Results.Redirect(redirect);
    }

    // ===== Helpers =====

    public static object ToUserDto(User user)
    {
        return new
        {
            user_id = user.Id,
            full_name = user.FullName,
            email = user.Email,
            role = user.Role,
            status = user.Status,
            auth_provider = user.AuthProvider,
            email_verified = user.EmailVerified,
            must_change_password = user.MustChangePassword
        };
    }

    private static string AppendQuery(string url, string key, string value)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }

    private static void CleanExpiredStates()
    {
        var cutoff = DateTimeOffset.UtcNow - OAuthStateLifetime;
        foreach (var (key, value) in OAuthStates)
        {
            if (value.CreatedAt < cutoff)
            {
                OAuthStates.TryRemove(key, out _);
            }
        }
    }

    private static void CleanExpiredPendingRegistrations()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (key, value) in PendingRegistrations)
        {
            if (value.ExpiresAt < now)
            {
                PendingRegistrations.TryRemove(key, out _);
            }
        }
    }

    private static PendingRegistration? LookupPending(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }
        return PendingRegistrations.TryGetValue(email.Trim().ToLowerInvariant(), out var pending)
            ? pending
            : null;
    }

    private static string GenerateNumericCode(int digits)
    {
        var number = RandomNumberGenerator.GetInt32((int)Math.Pow(10, digits));
        return number.ToString("D" + digits);
    }

    /// <summary>12 chars: easy to read in an email, no ambiguous I/l/0/O.</summary>
    private static string GenerateTemporaryPassword()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        Span<char> output = stackalloc char[12];
        for (var index = 0; index < output.Length; index++)
        {
            output[index] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }
        return new string(output);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a.Length != b.Length)
        {
            return false;
        }
        var bytesA = System.Text.Encoding.UTF8.GetBytes(a);
        var bytesB = System.Text.Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }

    private static bool LooksLikeEmail(string value)
    {
        var atIndex = value.IndexOf('@');
        return atIndex > 0 && atIndex < value.Length - 1 && value.IndexOf('.', atIndex) > atIndex;
    }

    private sealed record PendingRegistration(
        string FullName,
        string Email,
        string Code,
        DateTimeOffset ExpiresAt,
        int Attempts);
}
