using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api;

public static class UserManagementEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var group = api.MapGroup("/admin/users");

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{userId:guid}", UpdateAsync);
        group.MapDelete("/{userId:guid}", DeactivateAsync);
    }

    private static async Task<IResult> ListAsync(
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var users = await dbContext.Users
            .OrderBy(user => user.FullName)
            .Select(user => new
            {
                user_id = user.Id,
                full_name = user.FullName,
                username = user.Username,
                user.Email,
                user.Role,
                user.Status,
                created_at = user.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return ApiResults.Success(users);
    }

    private static async Task<IResult> CreateAsync(
        UserRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        PasswordHasher passwordHasher,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var normalizedUsername = NormalizeUsername(request.Username);
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(request.Email))
        {
            return ApiResults.Error("VALIDATION_ERROR", "Full name, username, and email are required.", StatusCodes.Status400BadRequest);
        }

        if (normalizedUsername.Length < 3)
        {
            return ApiResults.Error("VALIDATION_ERROR", "Username must be at least 3 characters.", StatusCodes.Status400BadRequest);
        }

        var lowerEmail = request.Email.Trim().ToLowerInvariant();
        var lowerUsername = normalizedUsername.ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(user => user.Email.ToLower() == lowerEmail, cancellationToken))
        {
            return ApiResults.Error("VALIDATION_ERROR", "Email is already registered.", StatusCodes.Status400BadRequest);
        }

        if (await dbContext.Users.AnyAsync(user => user.Username.ToLower() == lowerUsername, cancellationToken))
        {
            return ApiResults.Error("VALIDATION_ERROR", "Username is already taken.", StatusCodes.Status400BadRequest);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Username = normalizedUsername,
            Email = request.Email.Trim(),
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = NormalizeRole(request.Role),
            Status = string.IsNullOrWhiteSpace(request.Status) ? UserStatuses.Active : request.Status,
            CreatedAt = DateTimeOffset.UtcNow,
            AuthProvider = "email",
            EmailVerified = true
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(AuthEndpoints.ToUserDto(user));
    }

    private static async Task<IResult> UpdateAsync(
        Guid userId,
        UpdateUserRequest request,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        if (user is null)
        {
            return ApiResults.Error("NOT_FOUND", "User was not found.", StatusCodes.Status404NotFound);
        }

        user.FullName = string.IsNullOrWhiteSpace(request.FullName) ? user.FullName : request.FullName;
        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            var normalizedUsername = NormalizeUsername(request.Username);
            if (normalizedUsername.Length < 3)
            {
                return ApiResults.Error("VALIDATION_ERROR", "Username must be at least 3 characters.", StatusCodes.Status400BadRequest);
            }

            var lowerUsername = normalizedUsername.ToLowerInvariant();
            if (await dbContext.Users.AnyAsync(candidate => candidate.Id != user.Id && candidate.Username.ToLower() == lowerUsername, cancellationToken))
            {
                return ApiResults.Error("VALIDATION_ERROR", "Username is already taken.", StatusCodes.Status400BadRequest);
            }

            user.Username = normalizedUsername;
        }
        user.Role = string.IsNullOrWhiteSpace(request.Role) ? user.Role : NormalizeRole(request.Role);
        user.Status = string.IsNullOrWhiteSpace(request.Status) ? user.Status : request.Status;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(AuthEndpoints.ToUserDto(user));
    }

    private static async Task<IResult> DeactivateAsync(
        Guid userId,
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CurrentUserAccessor currentUserAccessor,
        CancellationToken cancellationToken)
    {
        var (_, error) = await currentUserAccessor.RequireRoleAsync(httpContext, dbContext, UserRoles.Administrator, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        if (user is null)
        {
            return ApiResults.Error("NOT_FOUND", "User was not found.", StatusCodes.Status404NotFound);
        }

        user.Status = UserStatuses.Inactive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(AuthEndpoints.ToUserDto(user));
    }

    private static string NormalizeRole(string role)
    {
        return role == UserRoles.Administrator ? UserRoles.Administrator : UserRoles.Student;
    }

    private static string NormalizeUsername(string? value)
    {
        return string.Join(
            " ",
            (value ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
