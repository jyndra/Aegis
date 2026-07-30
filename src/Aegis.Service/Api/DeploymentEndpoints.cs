using Aegis.Core.Interfaces;
using Aegis.Core.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aegis.Service.Api;

public static class DeploymentEndpoints
{
    public static void MapDeploymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/deployment")
                             .WithTags("Deployment");

        group.MapPost("/install", async (InstallOptions? options, IInstallerService installer, CancellationToken ct) =>
        {
            var result = await installer.InstallAsync(options ?? new InstallOptions(), ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/restore-policies", async (IInstallerService installer, CancellationToken ct) =>
        {
            bool success = await installer.RestorePoliciesAsync(null, ct);
            return success 
                ? Results.Ok(new { success = true, message = "Default filtering policies successfully restored." })
                : Results.StatusCode(500);
        });

        group.MapGet("/uninstall-status", async (IUninstallerService uninstaller, CancellationToken ct) =>
        {
            var (canUninstall, reason) = await uninstaller.CheckCanUninstallAsync(ct);
            return Results.Ok(new { canUninstall, reason });
        });

        group.MapPost("/uninstall", async (bool? forceConfirm, IUninstallerService uninstaller, CancellationToken ct) =>
        {
            var result = await uninstaller.UninstallAsync(null, forceConfirm ?? false, ct);
            if (!result.Success && result.BlockedByCommitmentDevice)
            {
                return Results.Problem(
                    statusCode: 403,
                    title: "Commitment Device Active",
                    detail: result.Message
                );
            }
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });
    }
}
