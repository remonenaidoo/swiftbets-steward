namespace SwiftBets.Steward.Application.Ports;

/// <summary>A rolling window of platform events Steward has observed, queryable by key (coupon, fixture) or topic.</summary>
public interface IEventLog
{
    Task AppendAsync(RecentEvent recentEvent, CancellationToken cancellationToken);

    Task<IReadOnlyList<RecentEvent>> RecentAsync(string subject, int limit, CancellationToken cancellationToken);

    /// <summary>True when an event of this type and key was already seen with a different event id.</summary>
    Task<bool> SeenWithDifferentIdAsync(string eventType, string key, string discriminator, Guid eventId, CancellationToken cancellationToken);
}
