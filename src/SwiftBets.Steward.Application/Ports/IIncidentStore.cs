using SwiftBets.Steward.Domain.Incidents;
using SwiftBets.Steward.Domain.Remediation;
using SwiftBets.Steward.Domain.Reports;

namespace SwiftBets.Steward.Application.Ports;

public interface IIncidentStore
{
    /// <summary>Opens the incident unless one with the same fingerprint is still unresolved; returns the incident that owns the fingerprint.</summary>
    Task<(Incident Incident, bool Created)> OpenOrGetAsync(Incident incident, CancellationToken cancellationToken);

    Task SetStatusAsync(Guid incidentId, IncidentStatus status, CancellationToken cancellationToken);

    Task RecordToolCallAsync(Guid incidentId, ToolCallRecord call, CancellationToken cancellationToken);

    Task SaveReportAsync(Guid incidentId, IncidentReport? report, IReadOnlyList<string> problems, IReadOnlyList<RemediationAction> actions, IncidentStatus status, CancellationToken cancellationToken);

    Task<IncidentDetails?> GetAsync(Guid incidentId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Incident>> ListAsync(int limit, CancellationToken cancellationToken);

    Task<RemediationAction?> GetActionAsync(Guid actionId, CancellationToken cancellationToken);

    /// <summary>Stores the new action state only if the stored one is still <paramref name="expected"/>.</summary>
    Task<bool> UpdateActionAsync(RemediationAction action, ActionStatus expected, CancellationToken cancellationToken);

    Task AuditAsync(Guid incidentId, Guid? actionId, string actor, string what, string detailJson, CancellationToken cancellationToken);
}
