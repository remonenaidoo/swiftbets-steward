namespace SwiftBets.Steward.Domain.Remediation;

/// <summary>A proposed remediation. It runs only after a named operator approves it, and exactly once.</summary>
public sealed record RemediationAction(
    Guid ActionId,
    Guid IncidentId,
    ActionType Type,
    string Target,
    string Rationale,
    ActionStatus Status,
    string? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? Outcome)
{
    public static RemediationAction Propose(Guid incidentId, ActionType type, string target, string rationale, DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), incidentId, type, target, rationale, ActionStatus.Pending, null, null, null);

    public bool RequiresExecution => Type != ActionType.NoAction;

    public RemediationAction? Approve(string operatorId, DateTimeOffset now) =>
        Status == ActionStatus.Pending ? this with { Status = ActionStatus.Approved, DecidedBy = operatorId, DecidedAt = now } : null;

    public RemediationAction? Reject(string operatorId, DateTimeOffset now) =>
        Status == ActionStatus.Pending ? this with { Status = ActionStatus.Rejected, DecidedBy = operatorId, DecidedAt = now } : null;

    public RemediationAction? Complete(bool succeeded, string outcome) =>
        Status == ActionStatus.Approved ? this with { Status = succeeded ? ActionStatus.Executed : ActionStatus.Failed, Outcome = outcome } : null;
}
