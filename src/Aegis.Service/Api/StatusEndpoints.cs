using Aegis.Core.Interfaces;

namespace Aegis.Service.Api;

public static class StatusEndpoints
{
    public static void MapStatusEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/status/report", async (IHealthReporter healthReporter, CancellationToken cancellationToken) =>
        {
            var reports = await healthReporter.GetStatusReportAsync(cancellationToken);
            bool isDegraded = reports.Any(r => !string.Equals(r.Status, "Healthy", StringComparison.OrdinalIgnoreCase));

            return Results.Ok(new
            {
                protectionState = isDegraded ? "Degraded" : "Protected",
                isLocked = true,
                lockDaysRemaining = 25,
                timestamp = DateTimeOffset.UtcNow,
                subsystems = reports.Select(r => new
                {
                    component = r.Component,
                    status = r.Status,
                    lastCheckedAt = r.LastCheckedAt,
                    details = r.DetailJson
                })
            });
        });
    }
}
