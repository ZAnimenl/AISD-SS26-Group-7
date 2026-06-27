using Backend.Contracts;
using Backend.Domain;
using Backend.Api;
using System.Text.Json;

namespace OjSharp.Tests.ApiContractTests;

public sealed class UserAccountContractTests
{
    [Fact]
    public void Register_request_only_contains_student_self_registration_fields()
    {
        var properties = typeof(RegisterRequest).GetProperties().Select(property => property.Name).ToArray();

        Assert.Contains(nameof(RegisterRequest.FullName), properties);
        Assert.Contains(nameof(RegisterRequest.Username), properties);
        Assert.Contains(nameof(RegisterRequest.Email), properties);
        Assert.Contains(nameof(RegisterRequest.Password), properties);
        Assert.DoesNotContain("Role", properties);
        Assert.DoesNotContain("Status", properties);
    }

    [Fact]
    public void Admin_user_request_can_create_administrator_accounts()
    {
        var request = new UserRequest(
            "New Admin",
            "new-admin",
            "new.admin@example.com",
            "password",
            UserRoles.Administrator,
            UserStatuses.Active);

        Assert.Equal(UserRoles.Administrator, request.Role);
        Assert.Equal(UserStatuses.Active, request.Status);
    }

    [Fact]
    public void User_dto_exposes_administrator_role_as_lowercase_role_property()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Ada Admin",
            Username = "ada-admin",
            Email = "admin@example.com",
            Role = UserRoles.Administrator,
            Status = UserStatuses.Active
        };

        var json = JsonSerializer.Serialize(AuthEndpoints.ToUserDto(user), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"role\":\"administrator\"", json);
        Assert.Contains("\"status\":\"active\"", json);
        Assert.Contains("\"username\":\"ada-admin\"", json);
        Assert.DoesNotContain("\"Role\"", json);
    }

    [Fact]
    public void Registration_code_delivery_response_hides_code_when_email_was_sent()
    {
        var response = new RegistrationCodeDeliveryResponse(
            Sent: true,
            ExpiresAt: new DateTimeOffset(2026, 6, 23, 12, 0, 0, TimeSpan.Zero),
            VerificationCode: null);

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        Assert.Contains("\"sent\":true", json);
        Assert.Contains("\"expires_at\"", json);
        Assert.Contains("\"verification_code\":null", json);
        Assert.DoesNotContain("123456", json);
    }

    [Fact]
    public void Registration_code_delivery_response_exposes_fallback_code_when_email_fails()
    {
        var response = new RegistrationCodeDeliveryResponse(
            Sent: false,
            ExpiresAt: new DateTimeOffset(2026, 6, 23, 12, 0, 0, TimeSpan.Zero),
            VerificationCode: "123456");

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        Assert.Contains("\"sent\":false", json);
        Assert.Contains("\"expires_at\"", json);
        Assert.Contains("\"verification_code\":\"123456\"", json);
    }
}
