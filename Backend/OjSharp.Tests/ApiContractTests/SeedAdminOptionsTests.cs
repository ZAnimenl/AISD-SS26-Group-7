using Backend.Configuration;

namespace OjSharp.Tests.ApiContractTests;

public sealed class SeedAdminOptionsTests
{
    [Fact]
    public void Validate_accepts_configured_email_and_password()
    {
        var options = new SeedAdminOptions
        {
            Email = "admin@example.com",
            Password = "strong-password"
        };

        options.Validate();
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("admin@example.com", "")]
    public void Validate_rejects_missing_seed_admin_values(string email, string password)
    {
        var options = new SeedAdminOptions
        {
            Email = email,
            Password = password
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
