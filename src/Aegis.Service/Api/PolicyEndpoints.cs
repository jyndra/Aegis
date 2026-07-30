namespace Aegis.Service.Api;

public static class PolicyEndpoints
{
    public static void MapPolicyEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/policy", () => Results.Ok(new
        {
            version = "1.0.0",
            rulesCount = 0
        }));
    }
}
