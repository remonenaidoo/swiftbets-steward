namespace SwiftBets.Steward.Application.Ports;

/// <summary>Read-only views of other services, returned as the JSON they serve.</summary>
public interface IPlatformInspector
{
    Task<string> CouponStateAsync(Guid couponId, CancellationToken cancellationToken);

    Task<string> MetricAsync(string metric, CancellationToken cancellationToken);

    IReadOnlyList<string> Metrics { get; }

    /// <summary>The wallet's latest ledger reconciliation run, as the wallet serves it.</summary>
    Task<string> LedgerReconciliationAsync(CancellationToken cancellationToken);

    /// <summary>The casino's recent reconciliation runs with one provider, as the casino serves them.</summary>
    Task<string> ProviderReconciliationAsync(string providerId, CancellationToken cancellationToken);
}
