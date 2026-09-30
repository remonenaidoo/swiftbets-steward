using SwiftBets.Steward.Application.Incidents;
using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Domain.Incidents;
using SwiftBets.Steward.Domain.Remediation;

namespace SwiftBets.Steward.Application.Tests;

public sealed class DecideActionHandlerTests
{
    [Fact]
    public async Task Approval_executes_the_action_once_and_resolves_the_incident()
    {
        var (handler, incidents, executor, action) = Build();

        var result = await handler.ApproveAsync(action.ActionId, "operator1", CancellationToken.None);

        result.Value.Status.ShouldBe(ActionStatus.Executed);
        executor.Executions.ShouldBe(1);
        incidents.Incidents[action.IncidentId].Status.ShouldBe(IncidentStatus.Resolved);
    }

    [Fact]
    public async Task Second_decision_on_the_same_action_is_refused()
    {
        var (handler, _, executor, action) = Build();
        await handler.ApproveAsync(action.ActionId, "operator1", CancellationToken.None);

        (await handler.ApproveAsync(action.ActionId, "operator2", CancellationToken.None)).Error!.Code.ShouldBe("action_already_decided");
        executor.Executions.ShouldBe(1);
    }

    private static (DecideActionHandler, MemoryIncidents, CountingExecutor, RemediationAction) Build()
    {
        var incidents = new MemoryIncidents();
        var incident = Incident.Open(IncidentKind.StuckCoupon, "c1", "stuck", DateTimeOffset.UtcNow) with { Status = IncidentStatus.AwaitingApproval };
        incidents.Incidents[incident.IncidentId] = incident;
        var action = RemediationAction.Propose(incident.IncidentId, ActionType.RefreshCoupon, Guid.NewGuid().ToString(), "rebuild", DateTimeOffset.UtcNow);
        incidents.Actions.Add(action);
        var executor = new CountingExecutor();
        return (new DecideActionHandler(incidents, executor, TimeProvider.System), incidents, executor, action);
    }

    private sealed class CountingExecutor : IRemediationExecutor
    {
        public int Executions { get; private set; }

        public Task<(bool Succeeded, string Outcome)> ExecuteAsync(RemediationAction action, CancellationToken cancellationToken)
        {
            Executions++;
            return Task.FromResult((true, "200: resettled"));
        }
    }
}
