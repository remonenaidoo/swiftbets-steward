using Prometheus;

namespace SwiftBets.Steward.Infrastructure;

/// <summary>What the Steward incidents dashboard plots: detection, diagnosis outcomes, remediation and model spend.</summary>
internal static class StewardMetrics
{
    public static readonly Counter IncidentsRaised = Metrics.CreateCounter(
        "swiftbets_steward_incidents_raised_total", "Incidents opened by the detectors.", new CounterConfiguration { LabelNames = ["kind"] });

    public static readonly Counter Diagnoses = Metrics.CreateCounter(
        "swiftbets_steward_diagnoses_total", "Diagnoses finished, by outcome (awaitingApproval, diagnosisFailed).", new CounterConfiguration { LabelNames = ["kind", "outcome"] });

    public static readonly Counter Remediations = Metrics.CreateCounter(
        "swiftbets_steward_remediations_total", "Operator decisions and executions, by action type and status.", new CounterConfiguration { LabelNames = ["type", "status"] });

    public static readonly Counter ModelCostUsd = Metrics.CreateCounter(
        "swiftbets_steward_model_cost_usd_total", "Model spend in US dollars.", new CounterConfiguration { LabelNames = ["model"] });

    public static readonly Counter ModelTokens = Metrics.CreateCounter(
        "swiftbets_steward_model_tokens_total", "Model tokens by kind (input, output, cache_read, cache_write).", new CounterConfiguration { LabelNames = ["model", "kind"] });
}
