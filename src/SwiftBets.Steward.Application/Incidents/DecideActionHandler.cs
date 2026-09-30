using SwiftBets.Contracts.Errors;
using SwiftBets.Contracts.Results;
using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Domain.Incidents;
using SwiftBets.Steward.Domain.Remediation;

namespace SwiftBets.Steward.Application.Incidents;

/// <summary>
/// An operator's decision on a proposed remediation. Approval executes it once (the action id is the idempotency key
/// at the target service); every decision and outcome is audited with who made it.
/// </summary>
public sealed class DecideActionHandler(IIncidentStore incidents, IRemediationExecutor executor, TimeProvider time)
{
    public async Task<Result<RemediationAction>> ApproveAsync(Guid actionId, string operatorId, CancellationToken cancellationToken)
    {
        var action = await incidents.GetActionAsync(actionId, cancellationToken);
        if (action?.Approve(operatorId, time.GetUtcNow()) is not { } approved)
        {
            return Missing(action);
        }

        if (!await incidents.UpdateActionAsync(approved, ActionStatus.Pending, cancellationToken))
        {
            return Error.Conflict("action_already_decided", "Another operator decided this action first.");
        }

        await incidents.AuditAsync(action.IncidentId, actionId, operatorId, "action_approved", $$"""{"type":"{{action.Type}}","target":"{{action.Target}}"}""", cancellationToken);
        var (succeeded, outcome) = approved.RequiresExecution ? await executor.ExecuteAsync(approved, cancellationToken) : (true, "acknowledged; nothing to execute");
        var completed = approved.Complete(succeeded, outcome)!;
        await incidents.UpdateActionAsync(completed, ActionStatus.Approved, cancellationToken);
        await incidents.AuditAsync(action.IncidentId, actionId, "steward", succeeded ? "action_executed" : "action_failed", System.Text.Json.JsonSerializer.Serialize(new { outcome }), cancellationToken);
        await ResolveIfDecidedAsync(action.IncidentId, cancellationToken);
        return Result.Success(completed);
    }

    public async Task<Result<RemediationAction>> RejectAsync(Guid actionId, string operatorId, CancellationToken cancellationToken)
    {
        var action = await incidents.GetActionAsync(actionId, cancellationToken);
        if (action?.Reject(operatorId, time.GetUtcNow()) is not { } rejected)
        {
            return Missing(action);
        }

        if (!await incidents.UpdateActionAsync(rejected, ActionStatus.Pending, cancellationToken))
        {
            return Error.Conflict("action_already_decided", "Another operator decided this action first.");
        }

        await incidents.AuditAsync(action.IncidentId, actionId, operatorId, "action_rejected", "{}", cancellationToken);
        await ResolveIfDecidedAsync(action.IncidentId, cancellationToken);
        return Result.Success(rejected);
    }

    private async Task ResolveIfDecidedAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var details = await incidents.GetAsync(incidentId, cancellationToken);
        if (details is not null && details.Actions.All(a => a.Status is not (ActionStatus.Pending or ActionStatus.Approved)))
        {
            await incidents.SetStatusAsync(incidentId, IncidentStatus.Resolved, cancellationToken);
        }
    }

    private static Error Missing(RemediationAction? action) =>
        action is null ? Error.NotFound("action_not_found", "No such action.") : Error.Conflict("action_already_decided", $"The action is already {action.Status}.");
}
