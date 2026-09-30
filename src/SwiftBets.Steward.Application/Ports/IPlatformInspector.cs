namespace SwiftBets.Steward.Application.Ports;

/// <summary>Read-only views of other services, returned as the JSON they serve.</summary>
public interface IPlatformInspector
{
    Task<string> CouponStateAsync(Guid couponId, CancellationToken cancellationToken);

    Task<string> MetricAsync(string metric, CancellationToken cancellationToken);

    IReadOnlyList<string> Metrics { get; }
}
