using SwiftBets.Steward.Application.Model;

namespace SwiftBets.Steward.Application.Agent;

/// <summary>The fixed tool list. Order and text never change at run time, so the tool prefix stays cacheable.</summary>
public static class ToolCatalog
{
    public const string RecentEvents = "get_recent_events";
    public const string CouponState = "get_coupon_state";
    public const string ServiceMetrics = "get_service_metrics";
    public const string SearchRunbooks = "search_runbooks";
    public const string SubmitReport = "submit_report";

    public static IReadOnlyList<ToolSpec> Build(IReadOnlyList<string> metrics) =>
    [
        new(RecentEvents, "Recent platform events Steward observed for a subject: a coupon id, a fixture id, or a topic name. Returns up to `limit` events, newest first, with their payloads.",
            """{"type":"object","properties":{"subject":{"type":"string","description":"Coupon id, fixture id, or topic name."},"limit":{"type":"integer","minimum":1,"maximum":50}},"required":["subject","limit"],"additionalProperties":false}"""),
        new(CouponState, "Settlement and payout state for one coupon: legs and their latest evaluated result, settlement versions, Redis progress, payments per version, dead letters.",
            """{"type":"object","properties":{"coupon_id":{"type":"string","description":"The coupon's UUID."}},"required":["coupon_id"],"additionalProperties":false}"""),
        new(ServiceMetrics, "Current value of a named platform metric (a fixed catalogue of Prometheus queries).",
            "{\"type\":\"object\",\"properties\":{\"metric\":{\"type\":\"string\",\"enum\":[" + string.Join(",", metrics.Select(m => $"\"{m}\"")) + "]}},\"required\":[\"metric\"],\"additionalProperties\":false}"),
        new(SearchRunbooks, "Search the platform's runbooks. Returns whole runbooks with the sections that matched; cite sections by runbook id and exact section heading.",
            """{"type":"object","properties":{"query":{"type":"string"}},"required":["query"],"additionalProperties":false}"""),
        new(SubmitReport, "Submit the final incident report. Every evidence item must quote, verbatim, a short excerpt of the output of one of your earlier tool calls, by that call's id. Cite only runbook sections you retrieved.",
            """{"type":"object","properties":{"summary":{"type":"string"},"severity":{"type":"string","enum":["low","medium","high","critical"]},"root_cause":{"type":"string"},"hypothesis":{"type":"string"},"confidence":{"type":"number","minimum":0,"maximum":1},"evidence":{"type":"array","items":{"type":"object","properties":{"tool_call_id":{"type":"string"},"claim":{"type":"string"},"excerpt":{"type":"string"}},"required":["tool_call_id","claim","excerpt"],"additionalProperties":false}},"runbook_citations":{"type":"array","items":{"type":"object","properties":{"runbook_id":{"type":"string"},"section":{"type":"string"}},"required":["runbook_id","section"],"additionalProperties":false}},"proposed_actions":{"type":"array","items":{"type":"object","properties":{"type":{"type":"string","enum":["no_action","refresh_coupon","suspend_market","replay_dead_letter"]},"target":{"type":"string"},"rationale":{"type":"string"}},"required":["type","target","rationale"],"additionalProperties":false}}},"required":["summary","severity","root_cause","hypothesis","confidence","evidence","runbook_citations","proposed_actions"],"additionalProperties":false}"""),
    ];
}
