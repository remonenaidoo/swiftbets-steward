using System.Globalization;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Settlement;
using SwiftBets.Steward.Application.Detection;
using SwiftBets.Steward.Infrastructure.Persistence;

namespace SwiftBets.Steward.Infrastructure.Messaging;

/// <summary>Logs settlements and checks each against those already seen: same coupon and version under a new event id is a duplicate.</summary>
public sealed class CouponSettledObserver(PostgresEventLog log, DetectionRules rules) : IEventHandler<CouponSettledV1>
{
    public async Task HandleAsync(ConsumedEvent<CouponSettledV1> message, CancellationToken cancellationToken)
    {
        var settled = message.Envelope.Payload;
        await rules.OnSettledAsync(settled.CouponId, settled.SettlementVersion, message.Envelope.Id, cancellationToken);
        await EventLogWriter.AppendAsync(log, Topics.CouponSettled, settled.CouponId.ToString(), message.Envelope, settled.SettlementVersion.ToString(CultureInfo.InvariantCulture), cancellationToken);
    }
}
