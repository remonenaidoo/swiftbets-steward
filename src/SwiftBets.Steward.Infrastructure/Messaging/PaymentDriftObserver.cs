using System.Globalization;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Payments;
using SwiftBets.Steward.Application.Detection;
using SwiftBets.Steward.Infrastructure.Persistence;

namespace SwiftBets.Steward.Infrastructure.Messaging;

/// <summary>The payments reconciliation found the provider and the ledger disagree: that is always an incident.</summary>
public sealed class PaymentDriftObserver(PostgresEventLog log, DetectionRules rules) : IEventHandler<PaymentDriftDetectedV1>
{
    public async Task HandleAsync(ConsumedEvent<PaymentDriftDetectedV1> message, CancellationToken cancellationToken)
    {
        var drift = message.Envelope.Payload;
        var subject = $"{drift.Provider}:{drift.Day:yyyy-MM-dd}";
        await EventLogWriter.AppendAsync(log, Topics.PaymentDriftDetected, subject, message.Envelope, null, cancellationToken);
        var net = string.Create(CultureInfo.InvariantCulture, $"{drift.NetDifference.MinorUnits / 100m:0.00} {drift.NetDifference.Currency}");
        await rules.OnPaymentDriftAsync(drift.Provider, drift.Day, drift.DriftCount, net, drift.Summary, cancellationToken);
    }
}
