using System.Security.Cryptography;
using Backend.Domain;
using Backend.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public sealed class AuthTokenService
{
    private static readonly Dictionary<string, Guid> Tokens = new(StringComparer.Ordinal);

    public string CreateToken(User user)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Tokens[token] = user.Id;
        return token;
    }

    public void RevokeToken(string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            Tokens.Remove(token);
        }
    }

    public async Task<User?> GetUserAsync(HttpContext httpContext, OjSharpDbContext dbContext, CancellationToken cancellationToken)
    {
        var token = GetBearerToken(httpContext);
        if (token is null || !Tokens.TryGetValue(token, out var userId))
        {
            return null;
        }

        return await dbContext.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public static string? GetBearerToken(HttpContext httpContext)
    {
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..].Trim()
            : null;
    }
}
