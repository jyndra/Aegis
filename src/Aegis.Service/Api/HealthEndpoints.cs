namespace Aegis.Service.Api;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/health", () => Results.Ok(new
        {
            status = "Healthy",
            version = "1.0.0",
            timestamp = DateTimeOffset.UtcNow
        }));
    }
}
