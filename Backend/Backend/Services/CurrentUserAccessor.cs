using Backend.Contracts;
using Backend.Domain;
using Backend.Persistence;

namespace Backend.Services;

public sealed class CurrentUserAccessor(AuthTokenService tokenService)
{
    public async Task<(User? User, IResult? Error)> RequireUserAsync(
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await tokenService.GetUserAsync(httpContext, dbContext, cancellationToken);
        if (user is null || user.Status != UserStatuses.Active)
        {
            return (null, ApiResults.Error("UNAUTHENTICATED", "Authentication is required.", StatusCodes.Status401Unauthorized));
        }

        return (user, null);
    }

    public async Task<(User? User, IResult? Error)> RequireRoleAsync(
        HttpContext httpContext,
        OjSharpDbContext dbContext,
        string role,
        CancellationToken cancellationToken)
    {
        var (user, error) = await RequireUserAsync(httpContext, dbContext, cancellationToken);
        if (error is not null || user is null)
        {
            return (null, error);
        }

        if (user.Role != role)
        {
            return (null, ApiResults.Error("FORBIDDEN", "The current user cannot access this resource.", StatusCodes.Status403Forbidden));
        }

        return (user, null);
    }
}
