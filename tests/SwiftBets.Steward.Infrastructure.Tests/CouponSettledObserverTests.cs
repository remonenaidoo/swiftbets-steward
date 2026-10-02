using Npgsql;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Money;
using SwiftBets.Contracts.Settlement;
using SwiftBets.Steward.Application.Detection;
using SwiftBets.Steward.Application.Incidents;
using SwiftBets.Steward.Domain.Incidents;
using SwiftBets.Steward.Infrastructure.Messaging;
using SwiftBets.Steward.Infrastructure.Persistence;
using SwiftBets.Steward.Infrastructure.Workers;

namespace SwiftBets.Steward.Infrastructure.Tests;

public sealed class CouponSettledObserverTests(PostgresFixture postgres)
{
    [Fact]
    public async Task A_v2_settlement_republished_under_a_new_event_id_raises_a_duplicate_settlement_incident()
    {
        var (observer, incidents) = await ObserverAsync();
        var settled = Settled();

        await observer.HandleAsync(Consumed(settled), TestContext.Current.CancellationToken);
        await observer.HandleAsync(Consumed(settled), TestContext.Current.CancellationToken);

        (await incidents.ListAsync(10, TestContext.Current.CancellationToken)).ShouldHaveSingleItem().Kind.ShouldBe(IncidentKind.DuplicateSettlement);
    }

    [Fact]
    public async Task The_same_v2_event_redelivered_is_not_a_duplicate()
    {
        var (observer, incidents) = await ObserverAsync();
        var consumed = Consumed(Settled());

        await observer.HandleAsync(consumed, TestContext.Current.CancellationToken);
        await observer.HandleAsync(consumed, TestContext.Current.CancellationToken);

        (await incidents.ListAsync(10, TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    private static CouponSettledV2 Settled() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 1, CouponOutcome.Won, new Money(1_000, "ZAR"), new Money(2_000, "ZAR"), [], DateTimeOffset.UtcNow);

    private static ConsumedEvent<CouponSettledV2> Consumed(CouponSettledV2 settled) =>
        new(EventEnvelope<CouponSettledV2>.Create(settled, DateTimeOffset.UtcNow, Guid.NewGuid().ToString("N")), Topics.CouponSettledV2, 0, 0, new Dictionary<string, string>());

    private async Task<(CouponSettledObserver, PostgresIncidentStore)> ObserverAsync()
    {
        var database = "steward_" + Guid.NewGuid().ToString("N")[..10];
        await using (var admin = new NpgsqlConnection(postgres.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin);
            await create.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = database }.ConnectionString;
        var entry = typeof(Program).Assembly.EntryPoint!.Invoke(null, [new[] { $"--ConnectionStrings:SbSteward={connectionString}" }]);
        (entry is Task<int> task ? await task : (int)entry!).ShouldBe(0);
        var dataSource = NpgsqlDataSource.Create(connectionString);
        var log = new PostgresEventLog(dataSource);
        var incidents = new PostgresIncidentStore(dataSource);
        return (new CouponSettledObserver(log, new DetectionRules(new RaiseIncidentHandler(incidents, new DiagnosisQueue(), TimeProvider.System), log)), incidents);
    }
}
