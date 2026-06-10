using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Backend.Configuration;
using Microsoft.Extensions.Options;

namespace Backend.Services;

public sealed class GoogleOAuthService(
    IOptions<GoogleOAuthOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<GoogleOAuthService> logger)
{
    private readonly GoogleOAuthOptions config = options.Value;

    public bool IsConfigured => config.IsConfigured;

    /// <summary>Build the URL the browser should redirect to in order to start the Google sign-in flow.</summary>
    public string BuildAuthorizationUrl(string state)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = config.ClientId,
            ["redirect_uri"] = config.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["access_type"] = "online",
            ["include_granted_scopes"] = "true",
            ["prompt"] = "select_account",
            ["state"] = state
        };
        var queryString = string.Join("&",
            query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return $"https://accounts.google.com/o/oauth2/v2/auth?{queryString}";
    }

    /// <summary>Exchange the authorization code Google returned for an access token, then fetch the user's profile.</summary>
    public async Task<GoogleUserProfile?> ExchangeCodeAndFetchProfileAsync(
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpClient = httpClientFactory.CreateClient();

            // Step 1: exchange code for access token
            var tokenForm = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = config.ClientId,
                ["client_secret"] = config.ClientSecret,
                ["redirect_uri"] = config.RedirectUri,
                ["grant_type"] = "authorization_code"
            });
            var tokenResponse = await httpClient.PostAsync(
                "https://oauth2.googleapis.com/token", tokenForm, cancellationToken);
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var error = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Google token exchange failed: {Status} {Body}", tokenResponse.StatusCode, error);
                return null;
            }
            var token = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                logger.LogError("Google token response missing access_token");
                return null;
            }

            // Step 2: fetch profile
            using var userInfoRequest = new HttpRequestMessage(
                HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
            userInfoRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
            var userInfoResponse = await httpClient.SendAsync(userInfoRequest, cancellationToken);
            if (!userInfoResponse.IsSuccessStatusCode)
            {
                var error = await userInfoResponse.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Google userinfo failed: {Status} {Body}", userInfoResponse.StatusCode, error);
                return null;
            }
            var profile = await userInfoResponse.Content.ReadFromJsonAsync<GoogleUserProfile>(cancellationToken);
            return profile;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Google OAuth exchange failed");
            return null;
        }
    }

    private sealed class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}

public sealed class GoogleUserProfile
{
    [JsonPropertyName("sub")]
    public string Sub { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("email_verified")]
    public bool EmailVerified { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("picture")]
    public string? Picture { get; set; }
}
