using Aegis.Core.Models;

namespace Aegis.Service.Api;

public static class HandshakeEndpoints
{
    public static void MapHandshakeEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/handshake", (HandshakeRequest request) => Results.Ok(new HandshakeResponse(
            Token: "stub-token",
            ExpiresInSeconds: 300,
            Nonce: Guid.NewGuid().ToString("N"),
            CurrentState: ProtectionState.Protected,
            IsLocked: true
        )));
    }
}
