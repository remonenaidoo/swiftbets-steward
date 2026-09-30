namespace SwiftBets.Steward.Domain.Reports;

public sealed record IncidentReport(
    string Summary,
    string Severity,
    string RootCause,
    string Hypothesis,
    double Confidence,
    IReadOnlyList<EvidenceItem> Evidence,
    IReadOnlyList<RunbookCitation> RunbookCitations,
    IReadOnlyList<ProposedAction> ProposedActions);
