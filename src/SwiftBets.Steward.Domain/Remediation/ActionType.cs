namespace SwiftBets.Steward.Domain.Remediation;

/// <summary>The only remediations Steward may propose. Anything else a model suggests is rejected by validation.</summary>
public enum ActionType
{
    NoAction,
    RefreshCoupon,
    SuspendMarket,
    ReplayDeadLetter,
}
