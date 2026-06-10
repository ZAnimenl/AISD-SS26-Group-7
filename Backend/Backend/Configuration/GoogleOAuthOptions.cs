namespace Backend.Configuration;

public sealed class GoogleOAuthOptions
{
    public const string SectionName = "GoogleOAuth";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Backend redirect URI registered in Google Cloud Console.</summary>
    public string RedirectUri { get; set; } = "http://localhost:5140/api/v1/auth/google/callback";

    /// <summary>Frontend page the backend redirects to after exchanging the code.
    /// The backend appends ?token=...&user=... to this URL.</summary>
    public string FrontendCallback { get; set; } = "http://localhost:3000/auth/google/callback";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
