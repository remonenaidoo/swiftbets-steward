using SwiftBets.Steward.Domain.Incidents;
using SwiftBets.Steward.Domain.Remediation;
using SwiftBets.Steward.Domain.Reports;

namespace SwiftBets.Steward.Application.Ports;

public sealed record IncidentDetails(Incident Incident, IncidentReport? Report, IReadOnlyList<string> ReportProblems, IReadOnlyList<ToolCallRecord> ToolCalls, IReadOnlyList<RemediationAction> Actions);
