namespace SwiftBets.Steward.Infrastructure.Platform;

/// <summary>The only Prometheus queries Steward can run. The model picks a name; it never writes PromQL.</summary>
public static class MetricCatalog
{
    public static IReadOnlyDictionary<string, string> Queries { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["payout_retries_last_minute"] = "sum by (rung) (increase(swiftbets_payout_retries_scheduled_total[1m]))",
        ["dead_letters_last_10_minutes"] = "sum by (topic) (increase(swiftbets_messages_consumed_total{outcome=\"dead_lettered\"}[10m]))",
        ["consumer_failures_last_5_minutes"] = "sum by (topic, outcome) (increase(swiftbets_messages_consumed_total{outcome=~\"transient_failure|processing_failure\"}[5m]))",
        ["messages_handled_per_second"] = "sum by (topic) (rate(swiftbets_messages_consumed_total{outcome=\"handled\"}[1m]))",
        ["outbox_pending"] = "sum by (service) (swiftbets_outbox_pending)",
        ["placement_p99_seconds"] = "histogram_quantile(0.99, sum by (le) (rate(http_request_duration_seconds_bucket{service=\"placement\",endpoint=\"/coupons\"}[5m])))",
        ["services_up"] = "up{job=\"swiftbets\"}",
    };
}
