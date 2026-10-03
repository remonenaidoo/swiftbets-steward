using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Casino;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Steward.Application.Detection;
using SwiftBets.Steward.Infrastructure.Persistence;

namespace SwiftBets.Steward.Infrastructure.Messaging;

/// <summary>A casino provider's day that does not match our ledger opens an incident; a matched day is only logged.</summary>
public sealed class ProviderDriftObserver(PostgresEventLog log, DetectionRules rules) : IEventHandler<ProviderReconciliationV1>
{
    public async Task HandleAsync(ConsumedEvent<ProviderReconciliationV1> message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        var run = message.Envelope.Payload;
        var subject = $"{run.ProviderId}:{run.BusinessDate:yyyy-MM-dd}";
        await EventLogWriter.AppendAsync(log, Topics.ProviderReconciliation, subject, message.Envelope, null, cancellationToken);
        if (run.Status == ReconciliationStatus.Drift)
        {
            await rules.OnProviderDriftAsync(run.ProviderId, run.BusinessDate,
                $"Casino reconciliation with {run.ProviderId} for {run.BusinessDate:yyyy-MM-dd}: {run.MissingOnOurSide} missing on our side, {run.MissingOnProviderSide} missing at the provider, drift {run.Drift.MinorUnits} {run.Drift.Currency}.",
                cancellationToken);
        }
    }
}
