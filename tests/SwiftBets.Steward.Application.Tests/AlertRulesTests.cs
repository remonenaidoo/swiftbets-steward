using SwiftBets.Steward.Application.Detection;
using SwiftBets.Steward.Domain.Incidents;

namespace SwiftBets.Steward.Application.Tests;

public sealed class AlertRulesTests
{
    // Every alert the platform's Prometheus rules can fire (swiftbets-platform compose/prometheus/rules). A new alert must be added here and mapped.
    private static readonly string[] PlatformAlerts =
    [
        "NotificationsNotSending", "NotificationsRejected", "PaymentsReconciliationDrift", "PaymentsReconciliationStale", "PaymentsSweepFailing",
        "PaymentsWebhooksRejected", "WalletLedgerDrift", "WalletReconciliationFailing", "WalletReconciliationStale",
    ];

    [Fact]
    public void Every_platform_alert_opens_an_incident_and_takes_its_subject_from_its_label()
    {
        PlatformAlerts.ShouldAllBe(a => AlertRules.Resolve(a, new Dictionary<string, string>()) != null);
        AlertRules.Resolve("PaymentsWebhooksRejected", new Dictionary<string, string> { ["provider"] = "paystack" }).ShouldBe((IncidentKind.PaymentsDegraded, "paystack"));
    }

    [Fact]
    public void An_alert_no_rule_knows_opens_nothing() =>
        AlertRules.Resolve("SomethingNew", new Dictionary<string, string>()).ShouldBeNull();
}
