using Backend.Contracts;
using Backend.Services.Grading;

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

        api.MapGet("/config", async (
            DockerRuntimeProbe dockerRuntimeProbe,
            CancellationToken cancellationToken) =>
        {
            var sandboxStatus = await dockerRuntimeProbe.CheckAsync(cancellationToken);
            return ApiResults.Success(new
            {
                features = new
                {
                    registration_enabled = true,
                    embedded_ai_agent_enabled = true,
                    ai_chat_enabled = true,
                    ai_inline_completion_enabled = false,
                    token_tracking_enabled = true,
                    multi_file_workspace_enabled = true,
                    real_sandbox_enabled = sandboxStatus.IsAvailable
                },
                supported_languages = new[] { "python", "javascript" },
                auth_method = "bearer_token",
                roles = new[] { "student", "administrator" }
            });
        });
    }
}
