namespace Backend.Configuration;

public sealed class SeedAdminOptions
{
    public const string SectionName = "SeedAdmin";

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            throw new InvalidOperationException("Seed admin email must be configured with SeedAdmin__Email.");
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            throw new InvalidOperationException("Seed admin password must be configured with SeedAdmin__Password.");
        }
    }
}
