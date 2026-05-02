using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api;

public static class AuthEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth");

        group.MapPost("/login", LoginAsync);
        group.MapGet("/me", MeAsync);
        group.MapPost("/logout", Logout);
        group.MapPost("/register", RegisterAsync);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        OjSharpDbContext dbContext,
        PasswordHasher passwordHasher,
        AuthTokenService tokenService,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(
            candidate => candidate.Email == request.Email && candidate.Status == UserStatuses.Active,
            cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return ApiResults.Error("UNAUTHENTICATED", "Invalid email or password.", StatusCodes.Status401Unauthorized);
        }

        var token = tokenService.CreateToken(user);
        return ApiResults.Success(new
        {
            token,
            user = ToUserDto(user)
        });
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

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        OjSharpDbContext dbContext,
        PasswordHasher passwordHasher,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Users.AnyAsync(user => user.Email == request.Email, cancellationToken))
        {
            return ApiResults.Error("VALIDATION_ERROR", "Email is already registered.", StatusCodes.Status400BadRequest);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = passwordHasher.Hash(request.Password),
            Role = UserRoles.Student,
            Status = UserStatuses.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResults.Success(ToUserDto(user));
    }

    public static object ToUserDto(User user)
    {
        return new
        {
            user_id = user.Id,
            full_name = user.FullName,
            email = user.Email,
            role = user.Role,
            status = user.Status
        };
    }
}
