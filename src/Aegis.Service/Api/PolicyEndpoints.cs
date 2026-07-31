using Aegis.Core.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Aegis.Service.Api;

public record AddWebsiteRequest(string Domain);
public record AddKeywordRequest(string Keyword, int Weight = 50);
public record AddRegexRequest(string Pattern, int Score = 50, string? Description = null);

public static class PolicyEndpoints
{
    public static void MapPolicyEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/policy")
                          .WithTags("Policy");

        group.MapGet("/", () => Results.Ok(new
        {
            version = "1.0.0",
            status = "Active"
        }));

        group.MapGet("/custom-rules", async (ICustomPolicyService policyService, CancellationToken ct) =>
        {
            var overview = await policyService.GetCustomRulesOverviewAsync(ct);
            return Results.Ok(overview);
        });

        group.MapPost("/custom-websites", async (AddWebsiteRequest request, ICustomPolicyService policyService, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Domain))
            {
                return Results.BadRequest(new { success = false, message = "Domain cannot be empty." });
            }

            bool added = await policyService.AddCustomWebsiteAsync(request.Domain, ct);
            return added
                ? Results.Ok(new { success = true, message = $"Custom website '{request.Domain}' added to blocklist." })
                : Results.BadRequest(new { success = false, message = "Failed to add domain to blocklist." });
        });

        group.MapPost("/custom-keywords", async (AddKeywordRequest request, ICustomPolicyService policyService, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Keyword))
            {
                return Results.BadRequest(new { success = false, message = "Keyword cannot be empty." });
            }

            bool added = await policyService.AddCustomKeywordAsync(request.Keyword, request.Weight, ct);
            return added
                ? Results.Ok(new { success = true, message = $"Custom keyword '{request.Keyword}' added." })
                : Results.BadRequest(new { success = false, message = "Failed to add custom keyword." });
        });

        group.MapPost("/custom-regex", async (AddRegexRequest request, ICustomPolicyService policyService, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Pattern))
            {
                return Results.BadRequest(new { success = false, message = "Regex pattern cannot be empty." });
            }

            try
            {
                bool added = await policyService.AddCustomRegexAsync(request.Pattern, request.Score, request.Description ?? "", ct);
                return added
                    ? Results.Ok(new { success = true, message = $"Custom regex rule '{request.Pattern}' added." })
                    : Results.BadRequest(new { success = false, message = "Failed to add custom regex rule." });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { success = false, message = ex.Message });
            }
        });

        group.MapDelete("/custom-rules/{id:long}", async (long id, ICustomPolicyService policyService, CancellationToken ct) =>
        {
            var (success, message) = await policyService.RemoveCustomRuleAsync(id, ct);
            return success
                ? Results.Ok(new { success, message })
                : Results.Problem(statusCode: 403, title: "Protection Ratchet Active", detail: message);
        });
    }
}
