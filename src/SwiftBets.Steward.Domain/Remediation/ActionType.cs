namespace SwiftBets.Steward.Domain.Remediation;

/// <summary>The only remediations Steward may propose. Anything else a model suggests is rejected by validation.</summary>
public enum ActionType
{
    NoAction,
    RefreshCoupon,
    SuspendMarket,
    ReplayDeadLetter,

    /// <summary>Stops all new bets (placement kill switch) while money cannot be trusted.</summary>
    EngageKillSwitch,

    /// <summary>Asks the payment provider about every open payment now, as the missed webhooks would have.</summary>
    ReplayPaymentWebhooks,

    /// <summary>Puts every parked payout back on the retry ladder once the wallet is healthy again.</summary>
    RedrivePayouts,
}
