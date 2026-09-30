using Npgsql;
using SwiftBets.BuildingBlocks.Testing;
using SwiftBets.Steward.Domain.Incidents;
using SwiftBets.Steward.Infrastructure.Persistence;
using SwiftBets.Steward.Infrastructure.Runbooks;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace SwiftBets.Steward.Infrastructure.Tests;

public sealed class StewardPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Runbook_search_returns_the_matching_runbook_whole_with_its_sections()
    {
        var store = await RunbooksAsync();

        var hits = await store.SearchAsync("coupon evaluated but not settled, redis progress lost", 3, TestContext.Current.CancellationToken);

        hits[0].RunbookId.ShouldBe("stuck-coupon");
        hits[0].AllSections.ShouldContain("Remediation");
        hits[0].Markdown.ShouldContain("refresh_coupon");
    }

    [Fact]
    public async Task Query_with_nothing_relevant_returns_no_runbook_rather_than_the_nearest_one()
    {
        var store = await RunbooksAsync();

        (await store.SearchAsync("zebra marmalade telescope", 3, TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Repeated_signal_for_an_open_incident_does_not_open_a_second_one()
    {
        var incidents = new PostgresIncidentStore(await DataSourceAsync());
        var first = Incident.Open(IncidentKind.PoisonMessage, "swiftbets.offer.result-published.v1.dev", "poison", DateTimeOffset.UtcNow);

        (await incidents.OpenOrGetAsync(first, TestContext.Current.CancellationToken)).Created.ShouldBeTrue();
        var (existing, created) = await incidents.OpenOrGetAsync(Incident.Open(IncidentKind.PoisonMessage, first.Subject, "again", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

        created.ShouldBeFalse();
        existing.IncidentId.ShouldBe(first.IncidentId);
    }

    [Fact]
    public async Task Signal_after_the_incident_is_resolved_opens_a_new_one()
    {
        var incidents = new PostgresIncidentStore(await DataSourceAsync());
        var first = Incident.Open(IncidentKind.WalletOutage, "wallet", "down", DateTimeOffset.UtcNow);
        await incidents.OpenOrGetAsync(first, TestContext.Current.CancellationToken);
        await incidents.SetStatusAsync(first.IncidentId, IncidentStatus.Resolved, TestContext.Current.CancellationToken);

        (await incidents.OpenOrGetAsync(Incident.Open(IncidentKind.WalletOutage, "wallet", "down again", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken)).Created.ShouldBeTrue();
    }

    private async Task<PgvectorRunbookStore> RunbooksAsync()
    {
        var store = new PgvectorRunbookStore(await DataSourceAsync(), new HashingEmbeddingGenerator(), minimumSimilarity: 0.99);
        await store.IngestAsync(RunbookLibrary.Load(), TestContext.Current.CancellationToken);
        return store;
    }

    private async Task<NpgsqlDataSource> DataSourceAsync()
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
        return NpgsqlDataSource.Create(connectionString);
    }
}
