namespace SwiftBets.Steward.Domain.Incidents;

public enum IncidentStatus
{
    Open,
    Diagnosing,
    AwaitingApproval,
    Resolved,
    DiagnosisFailed,
}
