using SwiftBets.Steward.Domain.Incidents;

namespace SwiftBets.Steward.Application.Detection;

/// <summary>
/// Every Prometheus alert the platform fires, mapped to the incident Steward opens for it. The subject comes from the
/// alert's own label where it has one, so two providers' alerts are two incidents. An alert missing from this table is
/// refused by the webhook and counted, never silently dropped; a test keeps it in step with the platform's rules.
/// </summary>
public static class AlertRules
{
    private static readonly Dictionary<string, (IncidentKind Kind, string? SubjectLabel, string DefaultSubject)> Map = new(StringComparer.Ordinal)
    {
        ["WalletLedgerDrift"] = (IncidentKind.LedgerDrift, "kind", "wallet-ledger"),
        ["WalletReconciliationFailing"] = (IncidentKind.LedgerDrift, null, "wallet-ledger"),
        ["WalletReconciliationStale"] = (IncidentKind.LedgerDrift, null, "wallet-ledger"),
        ["PaymentsReconciliationDrift"] = (IncidentKind.PaymentDrift, "provider", "payments"),
        ["PaymentsReconciliationStale"] = (IncidentKind.PaymentDrift, "provider", "payments"),
        ["PaymentsWebhooksRejected"] = (IncidentKind.PaymentsDegraded, "provider", "payments"),
        ["PaymentsSweepFailing"] = (IncidentKind.PaymentsDegraded, "provider", "payments"),
        ["NotificationsRejected"] = (IncidentKind.NotificationsDegraded, "template", "notifications"),
        ["NotificationsNotSending"] = (IncidentKind.NotificationsDegraded, null, "notifications"),
    };

    public static IReadOnlyCollection<string> Known => Map.Keys;

    public static (IncidentKind Kind, string Subject)? Resolve(string alertName, IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        if (!Map.TryGetValue(alertName, out var rule))
        {
            return null;
        }

        var subject = rule.SubjectLabel is { } label && labels.TryGetValue(label, out var value) && !string.IsNullOrWhiteSpace(value) ? value : rule.DefaultSubject;
        return (rule.Kind, subject);
    }
}
