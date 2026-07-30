using Aegis.Core.Interfaces;
using Aegis.Core.Models;

namespace Aegis.Service.Api;

public static class EvaluateEndpoints
{
    public static void MapEvaluateEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/evaluate", async (EvaluationRequest request, IRuleEngine ruleEngine, CancellationToken cancellationToken) =>
        {
            var result = await ruleEngine.EvaluateAsync(request, cancellationToken);
            return Results.Ok(result);
        });
    }
}
