using Backend.Configuration;

namespace OjSharp.Tests.ApiContractTests;

public sealed class SmtpOptionsTests
{
    [Fact]
    public void IsConfigured_requires_a_valid_port_and_sender_credentials()
    {
        var options = new SmtpOptions
        {
            Host = "smtp.example.test",
            Port = 587,
            Username = "mailer",
            Password = "app-password",
            FromAddress = "mailer@example.test"
        };

        Assert.True(options.IsConfigured);

        options.Port = 0;
        Assert.False(options.IsConfigured);
    }

    [Fact]
    public void EnableSsl_defaults_to_true_for_starttls_smtp()
    {
        Assert.True(new SmtpOptions().EnableSsl);
    }
}
