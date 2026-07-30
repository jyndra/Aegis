using Aegis.Core.Models;

namespace Aegis.Service.Api;

public static class EvaluateEndpoints
{
    public static void MapEvaluateEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/evaluate", (EvaluationRequest request) => Results.Ok(new EvaluationResult(
            Decision: FilterDecision.Allow,
            Reason: "Rule evaluation stub",
            Severity: FilterSeverity.Info,
            Action: "Allow",
            ComponentState: "Protected",
            RetryAfterSeconds: null
        )));
    }
}
