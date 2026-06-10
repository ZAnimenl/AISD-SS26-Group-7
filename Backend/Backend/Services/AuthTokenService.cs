using System.Collections.Concurrent;
using System.Security.Cryptography;
using Backend.Configuration;
using Backend.Domain;
using Backend.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public sealed class AuthTokenService(IOptions<AuthOptions> options)
{
    private static readonly ConcurrentDictionary<string, TokenEntry> Tokens = new(StringComparer.Ordinal);
    private readonly AuthOptions config = options.Value;

    public TokenIssueResult CreateToken(User user, bool rememberMe)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expiresAt = rememberMe
            ? DateTimeOffset.UtcNow.AddDays(config.RememberMeExpirationDays)
            : DateTimeOffset.UtcNow.AddHours(config.DefaultExpirationHours);
        Tokens[token] = new TokenEntry(user.Id, expiresAt);
        return new TokenIssueResult(token, expiresAt);
    }

    public void RevokeToken(string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            Tokens.TryRemove(token, out _);
        }
    }

    public async Task<User?> GetUserAsync(HttpContext httpContext, OjSharpDbContext dbContext, CancellationToken cancellationToken)
    {
        var token = GetBearerToken(httpContext);
        if (token is null || !Tokens.TryGetValue(token, out var entry))
        {
            return null;
        }

        if (entry.ExpiresAt < DateTimeOffset.UtcNow)
        {
            Tokens.TryRemove(token, out _);
            return null;
        }

        return await dbContext.Users.FirstOrDefaultAsync(user => user.Id == entry.UserId, cancellationToken);
    }

    public static string? GetBearerToken(HttpContext httpContext)
    {
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..].Trim()
            : null;
    }

    private sealed record TokenEntry(Guid UserId, DateTimeOffset ExpiresAt);
}

public sealed record TokenIssueResult(string Token, DateTimeOffset ExpiresAt);
