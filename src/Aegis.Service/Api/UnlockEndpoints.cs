namespace Aegis.Service.Api;

public static class UnlockEndpoints
{
    public static void MapUnlockEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/unlock/request", () => Results.Ok(new { status = "UnlockRequested", cooldownMinutes = 60 }));
        routes.MapPost("/unlock/advance", () => Results.Ok(new { status = "UnlockAdvanced", stage = 1 }));
        routes.MapPost("/unlock/cancel", () => Results.Ok(new { status = "UnlockCancelled" }));
    }
}
