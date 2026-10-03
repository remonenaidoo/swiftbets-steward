using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SwiftBets.Steward.Application.Agent;
using SwiftBets.Steward.Application.Model;
using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Domain.Incidents;
using SwiftBets.Steward.Domain.Remediation;
using SwiftBets.Steward.Domain.Runbooks;
using SwiftBets.Steward.Infrastructure.Model;
using SwiftBets.Steward.Infrastructure.Runbooks;

namespace SwiftBets.Steward.Infrastructure.Tests;

/// <summary>
/// For every alert class, the recorded transcript runs through the real agent loop, tool dispatch and report validator
/// against fixed platform answers: the report must validate (every excerpt quotes a tool's output, every citation was
/// retrieved) and propose the action the runbook calls for.
/// </summary>
public sealed class ReplaySuiteTests
{
    public static TheoryData<IncidentKind, string, ActionType> AlertClasses => new()
    {
        { IncidentKind.LedgerDrift, "wallet-ledger", ActionType.EngageKillSwitch },
        { IncidentKind.PaymentDrift, "simulator:2026-10-03", ActionType.NoAction },
        { IncidentKind.PaymentsDegraded, "simulator", ActionType.ReplayPaymentWebhooks },
        { IncidentKind.NotificationsDegraded, "account.verify-email", ActionType.NoAction },
        { IncidentKind.ProviderDrift, "sim-seamless:2026-10-03", ActionType.NoAction },
    };

    [Theory]
    [MemberData(nameof(AlertClasses))]
    public async Task Every_alert_class_replays_to_an_evidence_valid_report_with_the_runbook_action(IncidentKind kind, string subject, ActionType expected)
    {
        var incidents = new MemoryIncidents();
        var (incident, _) = await incidents.OpenOrGetAsync(Incident.Open(kind, subject, $"{kind} on {subject}", DateTimeOffset.UtcNow), CancellationToken.None);
        var agent = new DiagnosisAgent(new ScrubbingLanguageModel(new ReplayLanguageModel(Transcripts())), incidents, new TwoCustomers(), new FixedAnswers(), new KeywordRunbooks(),
            new NoSpend(), Options.Create(new StewardOptions()), TimeProvider.System, NullLogger<DiagnosisAgent>.Instance);

        var status = await agent.DiagnoseAsync(incident.IncidentId, TestContext.Current.CancellationToken);

        incidents.Saved.Problems.ShouldBeEmpty();
        status.ShouldBe(IncidentStatus.AwaitingApproval);
        incidents.Actions.ShouldHaveSingleItem().Type.ShouldBe(expected);
    }

    private static string Transcripts()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "transcripts")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new DirectoryNotFoundException("transcripts"), "transcripts");
    }

    private sealed class FixedAnswers : IPlatformInspector
    {
        public IReadOnlyList<string> Metrics => ["wallet_ledger_drifts", "payments_webhooks_rejected_last_15m", "notifications_by_status_last_15m"];

        public Task<string> CouponStateAsync(Guid couponId, CancellationToken cancellationToken) => Task.FromResult("{}");

        public Task<string> MetricAsync(string metric, CancellationToken cancellationToken) =>
            Task.FromResult($$"""{"metric":"{{metric}}","series":[{"labels":{},"value":"1"}]}""");

        public Task<string> LedgerReconciliationAsync(CancellationToken cancellationToken) =>
            Task.FromResult("""{"runId":"r-1","accountsChecked":40,"isClean":false,"drifts":[{"kind":"BalanceDrift","expected":5000,"actual":4500}]}""");

        public Task<string> ProviderReconciliationAsync(string providerId, CancellationToken cancellationToken) =>
            Task.FromResult($$"""[{"providerId":"{{providerId}}","status":"drift","missingOnOurSide":0,"missingOnProviderSide":1}]""");
    }

    private sealed class TwoCustomers : IEventLog
    {
        public Task AppendAsync(RecentEvent recentEvent, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<IReadOnlyList<RecentEvent>> RecentAsync(string subject, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RecentEvent>>(
            [
                new("topic", subject, "e", Guid.NewGuid(), DateTimeOffset.UtcNow, """{"punterId":"11111111-1111-1111-1111-111111111111","email":"someone@example.com"}"""),
                new("topic", subject, "e", Guid.NewGuid(), DateTimeOffset.UtcNow, """{"userId":"22222222-2222-2222-2222-222222222222"}"""),
            ]);

        public Task<bool> SeenWithDifferentIdAsync(string eventType, string key, string discriminator, Guid eventId, CancellationToken cancellationToken) => Task.FromResult(false);
    }

    /// <summary>The shipped runbooks, ranked by words shared with the query: enough to return the right one for the scripted searches.</summary>
    private sealed class KeywordRunbooks : IRunbookSearch
    {
        private static readonly IReadOnlyList<RunbookDocument> Library = RunbookLibrary.Load();

        public Task<IReadOnlyList<RunbookHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
        {
            var words = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return Task.FromResult<IReadOnlyList<RunbookHit>>([.. Library
                .Select(d => (d, score: words.Count(w => (d.Title + " " + d.RunbookId).Contains(w, StringComparison.OrdinalIgnoreCase))))
                .Where(x => x.score > 0)
                .OrderByDescending(x => x.score)
                .Take(limit)
                .Select(x => new RunbookHit(x.d.RunbookId, x.d.Title, [.. x.d.Sections.Select(s => s.Heading)], [.. x.d.Sections.Select(s => s.Heading)], x.d.Markdown, x.score))]);
        }
    }

    private sealed class NoSpend : ISpendLedger
    {
        public Task<decimal> MonthToDateUsdAsync(CancellationToken cancellationToken) => Task.FromResult(0m);

        public Task RecordAsync(Guid incidentId, string model, ModelUsage usage, decimal costUsd, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
