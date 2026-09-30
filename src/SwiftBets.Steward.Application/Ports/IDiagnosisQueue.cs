namespace SwiftBets.Steward.Application.Ports;

public interface IDiagnosisQueue
{
    ValueTask EnqueueAsync(Guid incidentId, CancellationToken cancellationToken);
}
