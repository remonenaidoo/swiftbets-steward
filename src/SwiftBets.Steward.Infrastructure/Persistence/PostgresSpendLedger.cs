using Dapper;
using Npgsql;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Steward.Application.Model;
using SwiftBets.Steward.Application.Ports;

namespace SwiftBets.Steward.Infrastructure.Persistence;

public sealed class PostgresSpendLedger(NpgsqlDataSource dataSource) : ISpendLedger
{
    private static readonly SqlResources Sql = SqlResources.For<PostgresSpendLedger>();

    public async Task<decimal> MonthToDateUsdAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(Sql.Get("Spend.MonthToDate"), cancellationToken: cancellationToken));
    }

    public async Task RecordAsync(Guid incidentId, string model, ModelUsage usage, decimal costUsd, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Spend.Record"), new
        {
            IncidentId = incidentId, Model = model, usage.InputTokens, usage.OutputTokens, usage.CacheReadTokens, usage.CacheWriteTokens, CostUsd = costUsd,
        }, cancellationToken: cancellationToken));

        StewardMetrics.ModelCostUsd.WithLabels(model).Inc((double)costUsd);
        StewardMetrics.ModelTokens.WithLabels(model, "input").Inc(usage.InputTokens);
        StewardMetrics.ModelTokens.WithLabels(model, "output").Inc(usage.OutputTokens);
        StewardMetrics.ModelTokens.WithLabels(model, "cache_read").Inc(usage.CacheReadTokens);
        StewardMetrics.ModelTokens.WithLabels(model, "cache_write").Inc(usage.CacheWriteTokens);
    }
}
