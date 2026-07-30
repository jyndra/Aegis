using Aegis.Core.Interfaces;

namespace Aegis.Service.Api;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/health", async (IHealthReporter healthReporter, CancellationToken cancellationToken) =>
        {
            var reports = await healthReporter.GetStatusReportAsync(cancellationToken);
            var serviceReport = reports.FirstOrDefault(r => string.Equals(r.Component, "Service", StringComparison.OrdinalIgnoreCase));
            string status = serviceReport?.Status ?? "Healthy";

            return Results.Ok(new
            {
                status,
                version = "1.0.0",
                timestamp = DateTimeOffset.UtcNow,
                componentCount = reports.Count
            });
        });
    }
}
