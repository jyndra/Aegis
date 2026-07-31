using Aegis.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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

        // API.md & Integration Test Aliases
        routes.MapPost("/integrity/check", async (IIntegrityEngine integrityEngine, CancellationToken cancellationToken) =>
        {
            var report = await integrityEngine.RunBootAuditAsync(cancellationToken);
            return Results.Ok(report);
        });

        routes.MapPost("/repair", async (IIntegrityEngine integrityEngine, CancellationToken cancellationToken) =>
        {
            var report = await integrityEngine.RunPeriodicAuditAsync(cancellationToken);
            return Results.Ok(report);
        });
    }
}
