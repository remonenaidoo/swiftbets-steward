using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Domain.Incidents;

namespace SwiftBets.Steward.Application.Incidents;

/// <summary>Detector entry point: opens an incident unless the same fingerprint is already open, then queues diagnosis.</summary>
public sealed class RaiseIncidentHandler(IIncidentStore incidents, IDiagnosisQueue diagnosis, TimeProvider time)
{
    public async Task<Incident> RaiseAsync(IncidentKind kind, string subject, string summary, CancellationToken cancellationToken)
    {
        var (incident, created) = await incidents.OpenOrGetAsync(Incident.Open(kind, subject, summary, time.GetUtcNow()), cancellationToken);
        if (created)
        {
            await incidents.AuditAsync(incident.IncidentId, null, "detector", "incident_opened", $$"""{"kind":"{{kind}}","subject":"{{subject}}"}""", cancellationToken);
            await diagnosis.EnqueueAsync(incident.IncidentId, cancellationToken);
        }

        return incident;
    }
}
