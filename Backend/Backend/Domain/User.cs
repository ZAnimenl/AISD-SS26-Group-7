namespace Backend.Domain;

public sealed class User
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = UserRoles.Student;

    public string Status { get; set; } = UserStatuses.Active;

    public DateTimeOffset CreatedAt { get; set; }

    // === Auth additions ===

    /// <summary>"email" or "google". Tracks how this user originally registered.</summary>
    public string AuthProvider { get; set; } = "email";

    /// <summary>Google subject id (sub) when AuthProvider = "google". Null otherwise.</summary>
    public string? GoogleId { get; set; }

    /// <summary>True once user confirmed their email via the verification link.
    /// Google-authenticated users start as true since Google already verified.</summary>
    public bool EmailVerified { get; set; }

    /// <summary>Single-use token sent in the verification email. Null after consumed.</summary>
    public string? EmailVerificationToken { get; set; }

    /// <summary>UTC timestamp at which EmailVerificationToken expires.</summary>
    public DateTimeOffset? EmailVerificationTokenExpiresAt { get; set; }

    /// <summary>Set to true after a forgot-password reset issues a temporary password.
    /// The next successful login forces the user through the change-password flow.</summary>
    public bool MustChangePassword { get; set; }

    public List<AssessmentSession> Sessions { get; set; } = [];
}
