using System.Text.Json;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Serialization;
using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Infrastructure.Persistence;

namespace SwiftBets.Steward.Infrastructure.Messaging;

internal static class EventLogWriter
{
    public static Task AppendAsync<T>(PostgresEventLog log, string topic, string key, EventEnvelope<T> envelope, string? discriminator, CancellationToken cancellationToken)
        where T : IEventContract =>
        log.AppendAsync(new RecentEvent(topic, key, envelope.Type, envelope.Id, envelope.OccurredAt, JsonSerializer.Serialize(envelope.Payload, ContractJson.Options)), discriminator, cancellationToken);
}
