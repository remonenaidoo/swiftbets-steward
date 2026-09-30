using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Steward.Infrastructure.Persistence;

namespace SwiftBets.Steward.Infrastructure.Messaging;

/// <summary>Records events Steward only needs as evidence (results, payouts, dead-lettered payouts).</summary>
public sealed class EventLogObserver<T>(PostgresEventLog log, Func<T, string> key, string topic) : IEventHandler<T>
    where T : IEventContract
{
    public Task HandleAsync(ConsumedEvent<T> message, CancellationToken cancellationToken) =>
        EventLogWriter.AppendAsync(log, topic, key(message.Envelope.Payload), message.Envelope, null, cancellationToken);
}
