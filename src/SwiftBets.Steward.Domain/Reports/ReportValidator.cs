using SwiftBets.Steward.Domain.Remediation;

namespace SwiftBets.Steward.Domain.Reports;

/// <summary>
/// The model reads; code verifies. A report is accepted only if every piece of evidence quotes the output of a tool
/// call that actually happened, every citation names a runbook section that was retrieved, and every proposed action
/// is on the allow-list. Anything else is returned as a problem, never silently kept.
/// </summary>
public static class ReportValidator
{
    private static readonly string[] Severities = ["low", "medium", "high", "critical"];

    public static IReadOnlyList<string> Validate(IncidentReport report, IReadOnlyList<ToolCallRecord> toolCalls, IReadOnlySet<string> retrievedSections)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(report.Summary) || string.IsNullOrWhiteSpace(report.RootCause))
        {
            problems.Add("summary and rootCause are required");
        }

        if (!Severities.Contains(report.Severity))
        {
            problems.Add($"severity must be one of {string.Join(", ", Severities)}");
        }

        if (report.Confidence is < 0 or > 1)
        {
            problems.Add("confidence must be between 0 and 1");
        }

        if (report.Evidence.Count == 0)
        {
            problems.Add("at least one piece of evidence is required");
        }

        var calls = toolCalls.ToDictionary(c => c.ToolCallId, StringComparer.Ordinal);
        foreach (var item in report.Evidence)
        {
            if (!calls.TryGetValue(item.ToolCallId, out var call))
            {
                problems.Add($"evidence cites tool call {item.ToolCallId}, which never happened");
            }
            else if (string.IsNullOrWhiteSpace(item.Excerpt) || !call.OutputJson.Contains(item.Excerpt, StringComparison.Ordinal))
            {
                problems.Add($"evidence excerpt \"{Truncate(item.Excerpt)}\" does not appear in the output of {item.ToolCallId} ({call.Name})");
            }
        }

        foreach (var citation in report.RunbookCitations)
        {
            if (!retrievedSections.Contains($"{citation.RunbookId}#{citation.Section}"))
            {
                problems.Add($"citation {citation.RunbookId}#{citation.Section} was not retrieved during diagnosis");
            }
        }

        foreach (var action in report.ProposedActions)
        {
            if (ParseAction(action.Type) is null)
            {
                problems.Add($"action type {action.Type} is not allowed");
            }
        }

        return problems;
    }

    public static ActionType? ParseAction(string type) => type switch
    {
        "no_action" => ActionType.NoAction,
        "refresh_coupon" => ActionType.RefreshCoupon,
        "suspend_market" => ActionType.SuspendMarket,
        "replay_dead_letter" => ActionType.ReplayDeadLetter,
        _ => null,
    };

    private static string Truncate(string value) => value.Length > 60 ? value[..60] + "..." : value;
}
