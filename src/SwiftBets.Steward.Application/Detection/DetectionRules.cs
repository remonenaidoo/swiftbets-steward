using System.Globalization;
using SwiftBets.Steward.Application.Incidents;
using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Domain.Incidents;

namespace SwiftBets.Steward.Application.Detection;

/// <summary>Turns platform signals into incidents. Each rule is deterministic; the model is only involved after an incident exists.</summary>
public sealed class DetectionRules(RaiseIncidentHandler raise, IEventLog events)
{
    /// <summary>Retry schedules per minute above which the wallet is treated as down.</summary>
    public const double WalletOutageRetriesPerMinute = 5;

    public Task OnStuckCouponAsync(Guid couponId, string reason, CancellationToken cancellationToken) =>
        raise.RaiseAsync(IncidentKind.StuckCoupon, couponId.ToString(), $"Reconciler reported coupon {couponId} as stuck: {reason}", cancellationToken);

    public Task OnDeadLetterAsync(string sourceTopic, string key, string reason, CancellationToken cancellationToken) =>
        raise.RaiseAsync(IncidentKind.PoisonMessage, sourceTopic, $"A message with key {key} from {sourceTopic} was dead-lettered: {reason}", cancellationToken);

    public async Task OnSettledAsync(Guid couponId, int version, Guid eventId, CancellationToken cancellationToken)
    {
        if (await events.SeenWithDifferentIdAsync("settlement.coupon-settled", couponId.ToString(), version.ToString(CultureInfo.InvariantCulture), eventId, cancellationToken))
        {
            await raise.RaiseAsync(IncidentKind.DuplicateSettlement, couponId.ToString(), $"Coupon {couponId} settlement v{version} was published more than once.", cancellationToken);
        }
    }

    /// <summary>One incident per provider and day: a rerun that finds the same drift joins the open incident.</summary>
    public Task OnPaymentDriftAsync(string provider, DateOnly day, int driftCount, string netDifference, string summary, CancellationToken cancellationToken) =>
        raise.RaiseAsync(IncidentKind.PaymentDrift, $"{provider}:{day:yyyy-MM-dd}",
            $"Payments reconciliation for {provider} on {day:yyyy-MM-dd} found {driftCount} drift(s), net {netDifference}: {summary}", cancellationToken);

    public async Task OnLadderRateAsync(double retriesPerMinute, CancellationToken cancellationToken)
    {
        if (retriesPerMinute >= WalletOutageRetriesPerMinute)
        {
            await raise.RaiseAsync(IncidentKind.WalletOutage, "wallet", $"Payout scheduled {retriesPerMinute:F0} wallet retries in the last minute.", cancellationToken);
        }
    }

    /// <summary>A firing Prometheus alert; false when no rule knows it, so the webhook can say so.</summary>
    public async Task<bool> OnAlertAsync(string alertName, IReadOnlyDictionary<string, string> labels, string summary, CancellationToken cancellationToken)
    {
        if (AlertRules.Resolve(alertName, labels) is not { } rule)
        {
            return false;
        }

        await raise.RaiseAsync(rule.Kind, rule.Subject, $"Alert {alertName}: {summary}", cancellationToken);
        return true;
    }

    /// <summary>The casino's daily reconciliation with a provider found transactions one side has and the other does not.</summary>
    public Task OnProviderDriftAsync(string providerId, DateOnly day, string summary, CancellationToken cancellationToken) =>
        raise.RaiseAsync(IncidentKind.ProviderDrift, $"{providerId}:{day:yyyy-MM-dd}", summary, cancellationToken);
}
