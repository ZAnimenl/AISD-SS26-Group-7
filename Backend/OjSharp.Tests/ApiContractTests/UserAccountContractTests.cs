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
            Email = "admin@example.com",
            Role = UserRoles.Administrator,
            Status = UserStatuses.Active
        };

        var json = JsonSerializer.Serialize(AuthEndpoints.ToUserDto(user), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"role\":\"administrator\"", json);
        Assert.Contains("\"status\":\"active\"", json);
        Assert.DoesNotContain("\"Role\"", json);
    }
}
