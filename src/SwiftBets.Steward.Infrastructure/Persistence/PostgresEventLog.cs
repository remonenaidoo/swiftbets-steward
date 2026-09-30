using Dapper;
using Npgsql;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Steward.Application.Ports;

namespace SwiftBets.Steward.Infrastructure.Persistence;

public sealed class PostgresEventLog(NpgsqlDataSource dataSource) : IEventLog
{
    private static readonly SqlResources Sql = SqlResources.For<PostgresEventLog>();

    public async Task AppendAsync(RecentEvent recentEvent, CancellationToken cancellationToken) => await AppendAsync(recentEvent, null, cancellationToken);

    public async Task AppendAsync(RecentEvent recentEvent, string? discriminator, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Events.Append"), new
        {
            recentEvent.Topic, recentEvent.Key, recentEvent.EventType, recentEvent.EventId, Discriminator = discriminator, recentEvent.OccurredAt, Payload = recentEvent.PayloadJson,
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<RecentEvent>> RecentAsync(string subject, int limit, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<(string Topic, string Key, string EventType, Guid? EventId, DateTime OccurredAt, string PayloadJson)>(
            new CommandDefinition(Sql.Get("Events.Recent"), new { Subject = subject, Limit = limit }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => new RecentEvent(r.Topic, r.Key, r.EventType, r.EventId, new DateTimeOffset(r.OccurredAt, TimeSpan.Zero), r.PayloadJson))];
    }

    public async Task<bool> SeenWithDifferentIdAsync(string eventType, string key, string discriminator, Guid eventId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(Sql.Get("Events.SeenWithDifferentId"),
            new { EventType = eventType, Key = key, Discriminator = discriminator, EventId = eventId }, cancellationToken: cancellationToken));
    }

    public async Task PruneAsync(DateTimeOffset before, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Events.Prune"), new { Before = before }, cancellationToken: cancellationToken));
    }
}
