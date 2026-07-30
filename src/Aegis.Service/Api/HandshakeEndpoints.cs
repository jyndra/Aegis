using Aegis.Core.Interfaces;
using Aegis.Core.Models;

namespace Aegis.Service.Api;

public static class HandshakeEndpoints
{
    public static void MapHandshakeEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/handshake", async (HandshakeRequest request, ISecurityService securityService, CancellationToken cancellationToken) =>
        {
            try
            {
                var response = await securityService.AuthenticateHandshakeAsync(request, cancellationToken);
                return Results.Ok(response);
            }
            catch (Core.Errors.AegisException ex)
            {
                return Results.Json(new { errorCode = ex.ErrorCode, message = ex.Message }, statusCode: 401);
            }
        });
    }
}
