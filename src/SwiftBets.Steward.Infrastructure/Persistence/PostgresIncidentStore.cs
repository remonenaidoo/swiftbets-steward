using System.Text.Json;
using Dapper;
using Npgsql;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Domain.Incidents;
using SwiftBets.Steward.Domain.Remediation;
using SwiftBets.Steward.Domain.Reports;

namespace SwiftBets.Steward.Infrastructure.Persistence;

public sealed class PostgresIncidentStore(NpgsqlDataSource dataSource) : IIncidentStore
{
    private static readonly SqlResources Sql = SqlResources.For<PostgresIncidentStore>();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<(Incident Incident, bool Created)> OpenOrGetAsync(Incident incident, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var created = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(Sql.Get("Incident.Open"), new
        {
            incident.IncidentId, Kind = incident.Kind.ToString(), incident.Subject, incident.Summary, Status = incident.Status.ToString(), incident.Fingerprint, incident.OpenedAt,
        }, cancellationToken: cancellationToken));
        if (created is not null)
        {
            return (incident, true);
        }

        var existing = await connection.QuerySingleOrDefaultAsync<IncidentRow>(new CommandDefinition(Sql.Get("Incident.FindOpen"), new { incident.Fingerprint }, cancellationToken: cancellationToken));
        return (existing?.ToDomain() ?? incident, false);
    }

    public async Task SetStatusAsync(Guid incidentId, IncidentStatus status, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Incident.SetStatus"), new { IncidentId = incidentId, Status = status.ToString() }, cancellationToken: cancellationToken));
    }

    public async Task RecordToolCallAsync(Guid incidentId, ToolCallRecord call, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Incident.RecordToolCall"),
            new { IncidentId = incidentId, call.ToolCallId, call.Name, Input = call.InputJson, Output = call.OutputJson }, cancellationToken: cancellationToken));
    }

    public async Task SaveReportAsync(Guid incidentId, IncidentReport? report, IReadOnlyList<string> problems, IReadOnlyList<RemediationAction> actions, IncidentStatus status, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(Sql.Get("Incident.SaveReport"), new
        {
            IncidentId = incidentId, Report = report is null ? null : JsonSerializer.Serialize(report, Json), Problems = JsonSerializer.Serialize(problems, Json),
        }, transaction);
        foreach (var action in actions)
        {
            await connection.ExecuteAsync(Sql.Get("Incident.InsertAction"), new
            {
                action.ActionId, action.IncidentId, Type = action.Type.ToString(), action.Target, action.Rationale, Status = action.Status.ToString(),
            }, transaction);
        }

        await connection.ExecuteAsync(Sql.Get("Incident.SetStatus"), new { IncidentId = incidentId, Status = status.ToString() }, transaction);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IncidentDetails?> GetAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        using var reader = await connection.QueryMultipleAsync(new CommandDefinition(Sql.Get("Incident.Get"), new { IncidentId = incidentId }, cancellationToken: cancellationToken));
        var incident = await reader.ReadSingleOrDefaultAsync<IncidentRow>();
        if (incident is null)
        {
            return null;
        }

        var report = await reader.ReadSingleOrDefaultAsync<(string? Report, string Problems)?>();
        var calls = (await reader.ReadAsync<ToolCallRecord>()).ToList();
        var actions = (await reader.ReadAsync<ActionRow>()).Select(a => a.ToDomain()).ToList();
        return new IncidentDetails(
            incident.ToDomain(),
            report?.Report is { } json ? JsonSerializer.Deserialize<IncidentReport>(json, Json) : null,
            report is { } r ? JsonSerializer.Deserialize<List<string>>(r.Problems, Json)! : [],
            calls,
            actions);
    }

    public async Task<IReadOnlyList<Incident>> ListAsync(int limit, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. (await connection.QueryAsync<IncidentRow>(new CommandDefinition(Sql.Get("Incident.List"), new { Limit = Math.Clamp(limit, 1, 200) }, cancellationToken: cancellationToken))).Select(r => r.ToDomain())];
    }

    public async Task<RemediationAction?> GetActionAsync(Guid actionId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return (await connection.QuerySingleOrDefaultAsync<ActionRow>(new CommandDefinition(Sql.Get("Incident.GetAction"), new { ActionId = actionId }, cancellationToken: cancellationToken)))?.ToDomain();
    }

    public async Task<bool> UpdateActionAsync(RemediationAction action, ActionStatus expected, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Incident.UpdateAction"), new
        {
            action.ActionId, Status = action.Status.ToString(), action.DecidedBy, action.DecidedAt, action.Outcome, Expected = expected.ToString(),
        }, cancellationToken: cancellationToken)) == 1;
    }

    public async Task AuditAsync(Guid incidentId, Guid? actionId, string actor, string what, string detailJson, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(Sql.Get("Incident.Audit"), new { IncidentId = incidentId, ActionId = actionId, Actor = actor, What = what, Detail = detailJson }, cancellationToken: cancellationToken));
    }

    private sealed record IncidentRow(Guid IncidentId, string Kind, string Subject, string Summary, string Status, DateTime OpenedAt)
    {
        public Incident ToDomain() => new(IncidentId, Enum.Parse<IncidentKind>(Kind), Subject, Summary, Enum.Parse<IncidentStatus>(Status), new DateTimeOffset(OpenedAt, TimeSpan.Zero));
    }

    private sealed record ActionRow(Guid ActionId, Guid IncidentId, string Type, string Target, string Rationale, string Status, string? DecidedBy, DateTime? DecidedAt, string? Outcome)
    {
        public RemediationAction ToDomain() => new(ActionId, IncidentId, Enum.Parse<ActionType>(Type), Target, Rationale, Enum.Parse<ActionStatus>(Status), DecidedBy,
            DecidedAt is { } at ? new DateTimeOffset(at, TimeSpan.Zero) : null, Outcome);
    }
}
