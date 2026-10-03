using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Domain.Incidents;
using SwiftBets.Steward.Domain.Remediation;
using SwiftBets.Steward.Domain.Reports;

namespace SwiftBets.Steward.Infrastructure.Tests;

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

