namespace SwiftBets.Steward.Domain.Incidents;

public enum IncidentKind
{
    StuckCoupon,
    WalletOutage,
    PoisonMessage,
    DuplicateSettlement,
    PaymentDrift,
    LedgerDrift,
    PaymentsDegraded,
    NotificationsDegraded,
    ProviderDrift,
}
