using Aegis.Core.Interfaces;

namespace Aegis.Service.Api;

public static class IntegrityEndpoints
{
    public static void MapIntegrityEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/integrity/status", async (IIntegrityEngine integrityEngine, CancellationToken cancellationToken) =>
        {
            var report = await integrityEngine.RunPeriodicAuditAsync(cancellationToken);
            return Results.Ok(report);
        });

        routes.MapPost("/integrity/audit", async (IIntegrityEngine integrityEngine, CancellationToken cancellationToken) =>
        {
            var report = await integrityEngine.RunBootAuditAsync(cancellationToken);
            return Results.Ok(report);
        });
    }
}
