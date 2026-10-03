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
    public async Task Signal_soon_after_a_failed_diagnosis_folds_into_that_incident()
    {
        var incidents = new PostgresIncidentStore(await DataSourceAsync());
        var first = Incident.Open(IncidentKind.WalletOutage, "wallet", "down", DateTimeOffset.UtcNow);
        await incidents.OpenOrGetAsync(first, TestContext.Current.CancellationToken);
        await incidents.SetStatusAsync(first.IncidentId, IncidentStatus.DiagnosisFailed, TestContext.Current.CancellationToken);

        var (existing, created) = await incidents.OpenOrGetAsync(Incident.Open(IncidentKind.WalletOutage, "wallet", "still down", DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task Retrieval_finds_the_right_runbook_in_the_top_three_for_at_least_nine_questions_in_ten()
    {
        var store = await RunbooksAsync();
        var cases = System.Text.Json.JsonSerializer.Deserialize<List<EvalCase>>(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "retrieval-eval.json"), TestContext.Current.CancellationToken), EvalJson)!;
        var misses = new List<string>();
        foreach (var c in cases)
        {
            var hits = await store.SearchAsync(c.Query, 3, TestContext.Current.CancellationToken);
            if (!hits.Any(h => h.RunbookId == c.Expected))
            {
                misses.Add($"{c.Query} -> wanted {c.Expected}, got [{string.Join(", ", hits.Select(h => h.RunbookId))}]");
            }
        }

        var hitRate = 1.0 - ((double)misses.Count / cases.Count);
        hitRate.ShouldBeGreaterThanOrEqualTo(0.9, $"hit@3 {hitRate:P0} over {cases.Count} questions; misses:\n{string.Join("\n", misses)}");
    }

    private static readonly System.Text.Json.JsonSerializerOptions EvalJson = new(System.Text.Json.JsonSerializerDefaults.Web);

    private sealed record EvalCase(string Query, string Expected);

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
