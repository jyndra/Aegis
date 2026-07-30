namespace Aegis.Service.Api;

public static class StatusEndpoints
{
    public static void MapStatusEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/status/report", () => Results.Ok(new
        {
            protectionState = "Protected",
            isLocked = true,
            daysRemaining = 25,
            dnsHealth = "Healthy",
            extensionHealth = "Healthy",
            serviceHealth = "Healthy"
        }));
    }
}
