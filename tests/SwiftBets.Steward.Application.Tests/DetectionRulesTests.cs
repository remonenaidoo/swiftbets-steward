using SwiftBets.Steward.Application.Detection;
using SwiftBets.Steward.Application.Incidents;
using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Domain.Incidents;

namespace SwiftBets.Steward.Application.Tests;

public sealed class DetectionRulesTests
{
    [Fact]
    public async Task A_payment_drift_opens_an_incident_for_the_provider_and_day_and_queues_its_diagnosis()
    {
        var incidents = new MemoryIncidents();
        var queue = new RecordingQueue();
        var rules = new DetectionRules(new RaiseIncidentHandler(incidents, queue, TimeProvider.System), new NoEvents());

        await rules.OnPaymentDriftAsync("simulator", new DateOnly(2026, 9, 30), 1, "123.45 ZAR", "1 MissingInLedger", TestContext.Current.CancellationToken);

        var incident = incidents.Incidents.Values.ShouldHaveSingleItem();
        (incident.Kind, incident.Subject).ShouldBe((IncidentKind.PaymentDrift, "simulator:2026-09-30"));
        incident.Summary.ShouldContain("found 1 drift(s), net 123.45 ZAR");
        queue.Queued.ShouldBe([incident.IncidentId]);
    }

    private sealed class RecordingQueue : IDiagnosisQueue
    {
        public List<Guid> Queued { get; } = [];

        public ValueTask EnqueueAsync(Guid incidentId, CancellationToken cancellationToken)
        {
            Queued.Add(incidentId);
            return ValueTask.CompletedTask;
        }
    }
}
