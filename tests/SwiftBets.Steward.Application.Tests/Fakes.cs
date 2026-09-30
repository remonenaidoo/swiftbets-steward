using SwiftBets.Steward.Application.Model;
using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Domain.Incidents;
using SwiftBets.Steward.Domain.Remediation;
using SwiftBets.Steward.Domain.Reports;

namespace SwiftBets.Steward.Application.Tests;

internal sealed class ScriptedModel(params IReadOnlyList<ModelBlock>[] turns) : ILanguageModel
{
    public int Calls { get; private set; }

    public Task<ModelTurn> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        var blocks = turns[Math.Min(Calls, turns.Length - 1)];
        Calls++;
        return Task.FromResult(new ModelTurn("claude-sonnet-5-5", blocks, "tool_use", new ModelUsage(1_000, 200, 0, 0)));
    }
}

internal sealed class MemoryIncidents : IIncidentStore
{
    public Dictionary<Guid, Incident> Incidents { get; } = [];

    public List<ToolCallRecord> Calls { get; } = [];

    public List<RemediationAction> Actions { get; } = [];

    public (IncidentReport? Report, IReadOnlyList<string> Problems) Saved { get; private set; }

    public Task<(Incident Incident, bool Created)> OpenOrGetAsync(Incident incident, CancellationToken cancellationToken)
    {
        Incidents[incident.IncidentId] = incident;
        return Task.FromResult((incident, true));
    }

    public Task SetStatusAsync(Guid incidentId, IncidentStatus status, CancellationToken cancellationToken)
    {
        Incidents[incidentId] = Incidents[incidentId] with { Status = status };
        return Task.CompletedTask;
    }

    public Task RecordToolCallAsync(Guid incidentId, ToolCallRecord call, CancellationToken cancellationToken)
    {
        Calls.Add(call);
        return Task.CompletedTask;
    }

    public Task SaveReportAsync(Guid incidentId, IncidentReport? report, IReadOnlyList<string> problems, IReadOnlyList<RemediationAction> actions, IncidentStatus status, CancellationToken cancellationToken)
    {
        Saved = (report, problems);
        Actions.AddRange(actions);
        return SetStatusAsync(incidentId, status, cancellationToken);
    }

    public Task<IncidentDetails?> GetAsync(Guid incidentId, CancellationToken cancellationToken) =>
        Task.FromResult<IncidentDetails?>(new IncidentDetails(Incidents[incidentId], Saved.Report, Saved.Problems ?? [], Calls, [.. Actions.Where(a => a.IncidentId == incidentId)]));

    public Task<IReadOnlyList<Incident>> ListAsync(int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Incident>>([.. Incidents.Values]);

    public Task<RemediationAction?> GetActionAsync(Guid actionId, CancellationToken cancellationToken) => Task.FromResult(Actions.FirstOrDefault(a => a.ActionId == actionId));

    public Task<bool> UpdateActionAsync(RemediationAction action, ActionStatus expected, CancellationToken cancellationToken)
    {
        var index = Actions.FindIndex(a => a.ActionId == action.ActionId);
        if (Actions[index].Status != expected)
        {
            return Task.FromResult(false);
        }

        Actions[index] = action;
        return Task.FromResult(true);
    }

    public Task AuditAsync(Guid incidentId, Guid? actionId, string actor, string what, string detailJson, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FixedPlatform : IPlatformInspector
{
    public IReadOnlyList<string> Metrics => ["payout_retries_last_minute"];

    public Task<string> CouponStateAsync(Guid couponId, CancellationToken cancellationToken) =>
        Task.FromResult($$"""{"couponId":"{{couponId}}","settlement":{"legCount":2,"settlementPending":true,"redisResolvedLegs":0},"payout":null}""");

    public Task<string> MetricAsync(string metric, CancellationToken cancellationToken) => Task.FromResult("""{"series":[]}""");
}

internal sealed class NoEvents : IEventLog
{
    public Task AppendAsync(RecentEvent recentEvent, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<RecentEvent>> RecentAsync(string subject, int limit, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<RecentEvent>>([]);

    public Task<bool> SeenWithDifferentIdAsync(string eventType, string key, string discriminator, Guid eventId, CancellationToken cancellationToken) => Task.FromResult(false);
}

internal sealed class OneRunbook : IRunbookSearch
{
    public Task<IReadOnlyList<RunbookHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RunbookHit>>([new("stuck-coupon", "Stuck coupon", ["Diagnosis"], ["Symptoms", "Diagnosis", "Remediation"], "# Stuck coupon", 0.03)]);
}

internal sealed class Budget(decimal spent) : ISpendLedger
{
    public Task<decimal> MonthToDateUsdAsync(CancellationToken cancellationToken) => Task.FromResult(spent);

    public Task RecordAsync(Guid incidentId, string model, ModelUsage usage, decimal costUsd, CancellationToken cancellationToken) => Task.CompletedTask;
}
