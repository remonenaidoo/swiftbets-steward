using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Settlement;
using SwiftBets.Steward.Application.Detection;
using SwiftBets.Steward.Infrastructure.Persistence;

namespace SwiftBets.Steward.Infrastructure.Messaging;

public sealed class StuckCouponObserver(PostgresEventLog log, DetectionRules rules) : IEventHandler<StuckCouponV1>
{
    public async Task HandleAsync(ConsumedEvent<StuckCouponV1> message, CancellationToken cancellationToken)
    {
        var stuck = message.Envelope.Payload;
        await EventLogWriter.AppendAsync(log, Topics.StuckCoupon, stuck.CouponId.ToString(), message.Envelope, null, cancellationToken);
        await rules.OnStuckCouponAsync(stuck.CouponId, stuck.Reason, cancellationToken);
    }
}
