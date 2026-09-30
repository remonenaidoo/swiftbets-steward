using System.Text.Json;
using Microsoft.Extensions.Logging;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Steward;
using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Domain.Incidents;
using SwiftBets.Steward.Domain.Remediation;
using SwiftBets.Steward.Domain.Reports;

namespace SwiftBets.Steward.Infrastructure.Persistence;

/// <summary>
/// Publishes incident and remediation changes after they are stored, for the dashboard's live view. These are
/// notifications, not the record: the dashboard re-reads the incident over HTTP, so a lost publish costs freshness,
/// never correctness, and a publish failure never fails the change it describes.
/// </summary>
public sealed partial class NotifyingIncidentStore(IIncidentStore inner, IEventPublisher publisher, TimeProvider time, ILogger<NotifyingIncidentStore> logger) : IIncidentStore
{
    public async Task<(Incident Incident, bool Created)> OpenOrGetAsync(Incident incident, CancellationToken cancellationToken)
    {
        var result = await inner.OpenOrGetAsync(incident, cancellationToken);
        if (result.Created)
        {
            var opened = result.Incident;
            await PublishAsync(Topics.IncidentRaised, opened.IncidentId, new IncidentRaisedV1(opened.IncidentId, Wire(opened.Kind), opened.Subject, opened.Summary, opened.OpenedAt));
        }

        return result;
    }

    public async Task SetStatusAsync(Guid incidentId, IncidentStatus status, CancellationToken cancellationToken)
    {
        await inner.SetStatusAsync(incidentId, status, cancellationToken);
        await PublishUpdatedAsync(incidentId, cancellationToken);
    }

    public async Task SaveReportAsync(Guid incidentId, IncidentReport? report, IReadOnlyList<string> problems, IReadOnlyList<RemediationAction> actions, IncidentStatus status, CancellationToken cancellationToken)
    {
        await inner.SaveReportAsync(incidentId, report, problems, actions, status, cancellationToken);
        await PublishUpdatedAsync(incidentId, cancellationToken);
    }

    public async Task<bool> UpdateActionAsync(RemediationAction action, ActionStatus expected, CancellationToken cancellationToken)
    {
        var updated = await inner.UpdateActionAsync(action, expected, cancellationToken);
        if (updated && action.Status is ActionStatus.Executed or ActionStatus.Failed)
        {
            await PublishAsync(Topics.RemediationExecuted, action.IncidentId, new RemediationExecutedV1(
                action.IncidentId, action.ActionId, JsonNamingPolicy.SnakeCaseLower.ConvertName(action.Type.ToString()), action.Target,
                action.Status == ActionStatus.Executed, action.Outcome ?? string.Empty, action.DecidedBy ?? "unknown", time.GetUtcNow()));
        }

        return updated;
    }

    public Task RecordToolCallAsync(Guid incidentId, ToolCallRecord call, CancellationToken cancellationToken) => inner.RecordToolCallAsync(incidentId, call, cancellationToken);

    public Task<IncidentDetails?> GetAsync(Guid incidentId, CancellationToken cancellationToken) => inner.GetAsync(incidentId, cancellationToken);

    public Task<IReadOnlyList<Incident>> ListAsync(int limit, CancellationToken cancellationToken) => inner.ListAsync(limit, cancellationToken);

    public Task<RemediationAction?> GetActionAsync(Guid actionId, CancellationToken cancellationToken) => inner.GetActionAsync(actionId, cancellationToken);

    public Task AuditAsync(Guid incidentId, Guid? actionId, string actor, string what, string detailJson, CancellationToken cancellationToken) =>
        inner.AuditAsync(incidentId, actionId, actor, what, detailJson, cancellationToken);

    private async Task PublishUpdatedAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        if (await inner.GetAsync(incidentId, cancellationToken) is not { } details)
        {
            return;
        }

        var incident = details.Incident;
        await PublishAsync(Topics.IncidentUpdated, incidentId, new IncidentUpdatedV1(
            incidentId, Wire(incident.Kind), incident.Subject, Wire(incident.Status), details.Report?.RootCause, time.GetUtcNow()));
    }

    private async Task PublishAsync<T>(string topic, Guid incidentId, T payload)
        where T : IEventContract
    {
        try
        {
            await publisher.PublishAsync(topic, incidentId.ToString(), EventEnvelope<T>.Create(payload, time.GetUtcNow(), CorrelationContext.CorrelationId ?? CorrelationContext.NewId()), CancellationToken.None);
        }
        catch (Exception ex)
        {
            LogPublishFailed(ex, topic, incidentId);
        }
    }

    private static string Wire<TEnum>(TEnum value)
        where TEnum : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    [LoggerMessage(Level = LogLevel.Warning, Message = "Publishing {Topic} for incident {IncidentId} failed; the dashboard will catch up on its next read")]
    private partial void LogPublishFailed(Exception exception, string topic, Guid incidentId);
}
