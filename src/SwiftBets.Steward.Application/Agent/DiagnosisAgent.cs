using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftBets.Steward.Application.Model;
using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Domain.Incidents;
using SwiftBets.Steward.Domain.Remediation;
using SwiftBets.Steward.Domain.Reports;

namespace SwiftBets.Steward.Application.Agent;

/// <summary>
/// The tool-calling loop. The model chooses which evidence to gather; code executes every tool, records exactly what
/// each returned, and accepts the final report only if its evidence and citations check out against those records.
/// One repair round is allowed; a report that still fails is stored as a failed diagnosis, never as a clean one.
/// </summary>
public sealed partial class DiagnosisAgent(
    ILanguageModel model,
    IIncidentStore incidents,
    IEventLog events,
    IPlatformInspector platform,
    IRunbookSearch runbooks,
    ISpendLedger spend,
    IOptions<StewardOptions> options,
    TimeProvider time,
    ILogger<DiagnosisAgent> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions SnakeCase = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<IncidentStatus> DiagnoseAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var details = await incidents.GetAsync(incidentId, cancellationToken) ?? throw new InvalidOperationException($"Incident {incidentId} not found.");
        var incident = details.Incident;
        await incidents.SetStatusAsync(incidentId, IncidentStatus.Diagnosing, cancellationToken);

        var tools = ToolCatalog.Build(platform.Metrics);
        var messages = new List<ModelMessage>
        {
            new(false, [ModelBlock.FromText($"Incident {incident.IncidentId}\nKind: {incident.Kind}\nSubject: {incident.Subject}\nDetector summary: {incident.Summary}")]),
        };
        var calls = new List<ToolCallRecord>();
        var retrieved = new HashSet<string>(StringComparer.Ordinal);
        var repaired = false;

        for (var turn = 0; turn < options.Value.MaxTurns; turn++)
        {
            if (await spend.MonthToDateUsdAsync(cancellationToken) >= options.Value.MonthlyBudgetUsd)
            {
                return await FailAsync(incidentId, "monthly model budget exhausted; diagnosis not attempted", cancellationToken);
            }

            var response = await model.CompleteAsync(new ModelRequest(incident.Kind.ToString(), StewardPrompt.System, tools, messages, options.Value.MaxTokens), cancellationToken);
            if (response.StopReason is "model_not_configured" or "refusal")
            {
                return await FailAsync(incidentId, response.StopReason == "refusal" ? "the model declined to diagnose this incident" : "no language model is configured (set ANTHROPIC_API_KEY and Steward:ModelProvider:Provider=anthropic)", cancellationToken);
            }

            await spend.RecordAsync(incidentId, response.Model, response.Usage, Cost(response), cancellationToken);
            messages.Add(new ModelMessage(true, response.Blocks));

            var toolUses = response.Blocks.Where(b => b.Kind == ModelBlockKind.ToolUse).ToList();
            if (toolUses.Count == 0)
            {
                messages.Add(new ModelMessage(false, [ModelBlock.FromText("Finish by calling submit_report with your findings.")]));
                continue;
            }

            var results = new List<ModelBlock>();
            foreach (var use in toolUses)
            {
                if (use.ToolName == ToolCatalog.SubmitReport)
                {
                    var (report, problems) = ParseAndValidate(use.InputJson!, calls, retrieved);
                    if (problems.Count == 0 || repaired)
                    {
                        return await CompleteAsync(incident, report, problems, cancellationToken);
                    }

                    repaired = true;
                    results.Add(ModelBlock.ToolResult(use.ToolUseId!, "Report rejected:\n- " + string.Join("\n- ", problems) + "\nFix these and call submit_report again.", isError: true));
                    continue;
                }

                var output = await RunToolAsync(use.ToolName!, use.InputJson!, retrieved, cancellationToken);
                var record = new ToolCallRecord(use.ToolUseId!, use.ToolName!, use.InputJson!, output);
                calls.Add(record);
                await incidents.RecordToolCallAsync(incidentId, record, cancellationToken);
                results.Add(ModelBlock.ToolResult(use.ToolUseId!, output));
            }

            messages.Add(new ModelMessage(false, results));
        }

        return await FailAsync(incidentId, $"no valid report after {options.Value.MaxTurns} turns", cancellationToken);
    }

    private async Task<string> RunToolAsync(string name, string inputJson, HashSet<string> retrieved, CancellationToken cancellationToken)
    {
        try
        {
            using var input = JsonDocument.Parse(inputJson);
            var root = input.RootElement;
            switch (name)
            {
                case ToolCatalog.RecentEvents:
                    var found = await events.RecentAsync(root.GetProperty("subject").GetString()!, Math.Clamp(root.GetProperty("limit").GetInt32(), 1, 50), cancellationToken);
                    return JsonSerializer.Serialize(found.Select(e => new { e.Topic, e.Key, e.EventType, e.EventId, e.OccurredAt, payload = JsonDocument.Parse(e.PayloadJson).RootElement }), Json);
                case ToolCatalog.CouponState:
                    return Guid.TryParse(root.GetProperty("coupon_id").GetString(), out var couponId)
                        ? await platform.CouponStateAsync(couponId, cancellationToken)
                        : """{"error":"coupon_id is not a UUID"}""";
                case ToolCatalog.ServiceMetrics:
                    return await platform.MetricAsync(root.GetProperty("metric").GetString()!, cancellationToken);
                case ToolCatalog.SearchRunbooks:
                    var hits = await runbooks.SearchAsync(root.GetProperty("query").GetString()!, 3, cancellationToken);
                    foreach (var hit in hits)
                    {
                        foreach (var section in hit.AllSections)
                        {
                            retrieved.Add($"{hit.RunbookId}#{section}");
                        }
                    }

                    return hits.Count == 0
                        ? """{"results":[],"note":"no runbook is relevant enough; say so rather than guessing"}"""
                        : JsonSerializer.Serialize(new { results = hits.Select(h => new { runbook_id = h.RunbookId, h.Title, matched_sections = h.MatchedSections, sections = h.AllSections, markdown = h.Markdown }) }, Json);
                default:
                    return JsonSerializer.Serialize(new { error = $"unknown tool {name}" }, Json);
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or HttpRequestException)
        {
            LogToolFailed(ex, name);
            return JsonSerializer.Serialize(new { error = $"{name} failed: {ex.Message}" }, Json);
        }
    }

    private static (IncidentReport? Report, IReadOnlyList<string> Problems) ParseAndValidate(string inputJson, IReadOnlyList<ToolCallRecord> calls, IReadOnlySet<string> retrieved)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<RawReport>(inputJson, SnakeCase)!;
            var report = new IncidentReport(raw.Summary, raw.Severity, raw.RootCause, raw.Hypothesis, raw.Confidence,
                [.. raw.Evidence.Select(e => new EvidenceItem(e.ToolCallId, e.Claim, e.Excerpt))],
                [.. raw.RunbookCitations.Select(c => new RunbookCitation(c.RunbookId, c.Section))],
                [.. raw.ProposedActions.Select(a => new ProposedAction(a.Type, a.Target, a.Rationale))]);
            return (report, ReportValidator.Validate(report, calls, retrieved));
        }
        catch (JsonException ex)
        {
            return (null, [$"report is not valid JSON for the submit_report schema: {ex.Message}"]);
        }
    }

    private async Task<IncidentStatus> CompleteAsync(Incident incident, IncidentReport? report, IReadOnlyList<string> problems, CancellationToken cancellationToken)
    {
        var status = report is not null && problems.Count == 0 ? IncidentStatus.AwaitingApproval : IncidentStatus.DiagnosisFailed;
        IReadOnlyList<RemediationAction> actions = status == IncidentStatus.AwaitingApproval
            ? [.. report!.ProposedActions.Select(a => RemediationAction.Propose(incident.IncidentId, ReportValidator.ParseAction(a.Type)!.Value, a.Target, a.Rationale, time.GetUtcNow()))]
            : [];
        await incidents.SaveReportAsync(incident.IncidentId, report, problems, actions, status, cancellationToken);
        LogDiagnosed(incident.IncidentId, status, problems.Count);
        return status;
    }

    private async Task<IncidentStatus> FailAsync(Guid incidentId, string reason, CancellationToken cancellationToken)
    {
        await incidents.SaveReportAsync(incidentId, null, [reason], [], IncidentStatus.DiagnosisFailed, cancellationToken);
        LogDiagnosed(incidentId, IncidentStatus.DiagnosisFailed, 1);
        return IncidentStatus.DiagnosisFailed;
    }

    private decimal Cost(ModelTurn turn)
    {
        if (!options.Value.Pricing.TryGetValue(turn.Model, out var price) || price.Length != 4)
        {
            return 0m;
        }

        var u = turn.Usage;
        return ((u.InputTokens * price[0]) + (u.OutputTokens * price[1]) + (u.CacheWriteTokens * price[2]) + (u.CacheReadTokens * price[3])) / 1_000_000m;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tool {Tool} failed")]
    private partial void LogToolFailed(Exception exception, string tool);

    [LoggerMessage(Level = LogLevel.Information, Message = "Incident {IncidentId} diagnosed: {Status} ({Problems} validation problems)")]
    private partial void LogDiagnosed(Guid incidentId, IncidentStatus status, int problems);

    private sealed record RawReport(string Summary, string Severity, string RootCause, string Hypothesis, double Confidence,
        IReadOnlyList<RawEvidence> Evidence, IReadOnlyList<RawCitation> RunbookCitations, IReadOnlyList<RawAction> ProposedActions);

    private sealed record RawEvidence(string ToolCallId, string Claim, string Excerpt);

    private sealed record RawCitation(string RunbookId, string Section);

    private sealed record RawAction(string Type, string Target, string Rationale);
}
