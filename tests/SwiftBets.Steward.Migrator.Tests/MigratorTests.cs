using Dapper;
using Npgsql;
using SwiftBets.BuildingBlocks.Testing;

[assembly: AssemblyFixture(typeof(PostgresFixture))]

namespace SwiftBets.Steward.Migrator.Tests;

public sealed class MigratorTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrator_enables_pgvector_creates_the_schema_and_is_idempotent()
    {
        string[] args = [$"--ConnectionStrings:SbSteward={postgres.ConnectionString}"];

        (await RunAsync(args)).ShouldBe(0);
        (await RunAsync(args)).ShouldBe(0);

        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM pg_extension WHERE extname = 'vector'")).ShouldBe(1);
        (await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name = 'steward'")).ShouldBe(1);
    }

    [Fact]
    public async Task Missing_connection_string_fails_with_a_usage_code() =>
        (await RunAsync([])).ShouldBe(2);

    private static async Task<int> RunAsync(string[] args)
    {
        var result = typeof(Program).Assembly.EntryPoint!.Invoke(null, [args]);
        return result is Task<int> task ? await task : (int)result!;
    }
}
