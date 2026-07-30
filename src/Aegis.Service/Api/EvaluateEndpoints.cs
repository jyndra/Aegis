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

        routes.MapPost("/evaluate/ai/text", async (AiTextRequest req, IAiTextClassifier classifier, CancellationToken cancellationToken) =>
        {
            var result = await classifier.ClassifyTextAsync(req.Text, cancellationToken);
            return Results.Ok(result);
        });

        routes.MapPost("/evaluate/ai/image", async (AiImageRequest req, IAiImageClassifier classifier, CancellationToken cancellationToken) =>
        {
            byte[]? bytes = null;
            if (!string.IsNullOrWhiteSpace(req.Base64Image))
            {
                try { bytes = Convert.FromBase64String(req.Base64Image); } catch { }
            }
            var result = await classifier.ClassifyImageBytesAsync(bytes, cancellationToken);
            return Results.Ok(result);
        });
    }

    public record AiTextRequest(string? Text);
    public record AiImageRequest(string? Base64Image);
}
