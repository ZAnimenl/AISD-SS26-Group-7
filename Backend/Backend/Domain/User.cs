namespace Backend.Domain;

public sealed class User
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = UserRoles.Student;

    public string Status { get; set; } = UserStatuses.Active;

    public DateTimeOffset CreatedAt { get; set; }

    public List<AssessmentSession> Sessions { get; set; } = [];
}
