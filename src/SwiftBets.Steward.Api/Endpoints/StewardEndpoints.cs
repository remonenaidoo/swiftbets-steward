using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Serialization;
using SwiftBets.Steward.Application.Incidents;
using SwiftBets.Steward.Application.Ports;

namespace SwiftBets.Steward.Api.Endpoints;

public static class StewardEndpoints
{
    public static IEndpointRouteBuilder MapStewardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var api = endpoints.MapGroup("/").RequireAuthorization(Roles.Operator);

        api.MapGet("/incidents", async (IIncidentStore incidents, int? limit, CancellationToken cancellationToken) =>
            Results.Json(await incidents.ListAsync(limit ?? 50, cancellationToken), ContractJson.Options));

        api.MapGet("/incidents/{incidentId:guid}", async (Guid incidentId, IIncidentStore incidents, HttpContext context, CancellationToken cancellationToken) =>
            await incidents.GetAsync(incidentId, cancellationToken) is { } details
                ? Results.Json(details, ContractJson.Options)
                : Error.NotFound("incident_not_found", "No such incident.").ToHttpResult(context));

        api.MapPost("/actions/{actionId:guid}/approve", async (Guid actionId, DecideActionHandler decide, HttpContext context, CancellationToken cancellationToken) =>
            (await decide.ApproveAsync(actionId, Operator(context), cancellationToken)).ToHttpResult(context));

        api.MapPost("/actions/{actionId:guid}/reject", async (Guid actionId, DecideActionHandler decide, HttpContext context, CancellationToken cancellationToken) =>
            (await decide.RejectAsync(actionId, Operator(context), cancellationToken)).ToHttpResult(context));

        api.MapPost("/drills/{fault}", async (string fault, IFaultDriver faults, IHostEnvironment environment, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (environment.IsProduction())
            {
                return Error.Validation("drills_disabled", "Fault drills are refused in Production.").ToHttpResult(context);
            }

            return faults.Faults.Contains(fault)
                ? Results.Ok(new { fault, injected = await faults.InjectAsync(fault, cancellationToken) })
                : Error.NotFound("unknown_fault", $"Faults: {string.Join(", ", faults.Faults)}.").ToHttpResult(context);
        });

        api.MapGet("/runbooks/search", async (string q, IRunbookSearch runbooks, CancellationToken cancellationToken) =>
            Results.Json((await runbooks.SearchAsync(q, 3, cancellationToken)).Select(h => new { h.RunbookId, h.Title, h.MatchedSections, h.Score }), ContractJson.Options));

        api.MapGet("/spend", async (ISpendLedger spend, CancellationToken cancellationToken) =>
            Results.Ok(new { monthToDateUsd = await spend.MonthToDateUsdAsync(cancellationToken) }));

        return endpoints;
    }

    private static string Operator(HttpContext context) => context.User.FindFirst("sub")?.Value ?? "unknown";
}
