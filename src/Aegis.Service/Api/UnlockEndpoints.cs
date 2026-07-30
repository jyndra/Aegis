using Aegis.Core.Interfaces;

namespace Aegis.Service.Api;

public record InitiateUnlockRequest(int Stage);
public record ConfirmUnlockRequest(int Stage, string ConfirmationChallenge);

public static class UnlockEndpoints
{
    public static void MapUnlockEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/unlock/status", async (ICommitLockEngine lockEngine, CancellationToken cancellationToken) =>
        {
            var state = await lockEngine.GetLockStateAsync(cancellationToken);
            return Results.Ok(state);
        });

        routes.MapPost("/unlock/initiate", async (InitiateUnlockRequest request, ICommitLockEngine lockEngine, CancellationToken cancellationToken) =>
        {
            var progress = await lockEngine.InitiateUnlockStageAsync(request.Stage, null, cancellationToken);
            return Results.Ok(progress);
        });

        routes.MapPost("/unlock/confirm", async (ConfirmUnlockRequest request, ICommitLockEngine lockEngine, CancellationToken cancellationToken) =>
        {
            var progress = await lockEngine.InitiateUnlockStageAsync(request.Stage, request.ConfirmationChallenge, cancellationToken);
            return Results.Ok(progress);
        });
    }
}
