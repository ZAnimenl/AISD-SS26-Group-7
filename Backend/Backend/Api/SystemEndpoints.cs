using Backend.Contracts;

namespace Backend.Api;

public static class SystemEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/health", () => ApiResults.Success(new
        {
            status = "healthy",
            checked_at = DateTimeOffset.UtcNow
        }));

        api.MapGet("/config", () => ApiResults.Success(new
        {
            features = new
            {
                registration_enabled = true,
                ai_chat_enabled = true,
                ai_inline_completion_enabled = false,
                multi_file_workspace_enabled = false,
                real_sandbox_enabled = true
            },
            supported_languages = new[] { "python", "javascript", "typescript" },
            auth_method = "bearer_token",
            roles = new[] { "student", "administrator" }
        }));
    }
}
