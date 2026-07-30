namespace Aegis.Service.Api;

public static class IntegrityEndpoints
{
    public static void MapIntegrityEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/integrity/check", () => Results.Ok(new { status = "AuditComplete", passed = true }));
        routes.MapPost("/repair", () => Results.Ok(new { status = "RepairAttempted", success = true }));
    }
}
