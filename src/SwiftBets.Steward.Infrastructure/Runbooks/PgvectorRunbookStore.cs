using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Npgsql;
using SwiftBets.BuildingBlocks.Persistence;
using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Domain.Runbooks;

namespace SwiftBets.Steward.Infrastructure.Runbooks;

/// <summary>
/// Small-to-big hybrid retrieval: sections are indexed (pgvector embeddings and Postgres full text), the two ranked
/// lists are fused with reciprocal rank fusion, and the whole parent runbook is returned with the sections that
/// matched. A runbook needs a full-text match or a vector similarity above the floor to be returned at all.
/// </summary>
public sealed class PgvectorRunbookStore(NpgsqlDataSource dataSource, IEmbeddingGenerator embeddings, double minimumSimilarity) : IRunbookSearch
{
    private static readonly SqlResources Sql = SqlResources.For<PgvectorRunbookStore>();

    public async Task<int> IngestAsync(IReadOnlyList<RunbookDocument> documents, CancellationToken cancellationToken)
    {
        var embedded = 0;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        foreach (var document in documents)
        {
            await connection.ExecuteAsync(Sql.Get("Runbooks.Upsert"), new { document.RunbookId, document.Title, document.Markdown });
            foreach (var section in document.Sections)
            {
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{embeddings.GetType().Name}|{document.Title}|{section.Heading}|{section.Text}")));
                if (await connection.ExecuteScalarAsync<string?>(Sql.Get("Runbooks.ChunkHash"), new { document.RunbookId, Section = section.Heading }) == hash)
                {
                    continue;
                }

                var vector = await embeddings.EmbedAsync($"{document.Title}. {section.Heading}. {section.Text}", cancellationToken);
                await connection.ExecuteAsync(Sql.Get("Runbooks.UpsertChunk"), new
                {
                    document.RunbookId, Section = section.Heading, document.Title, Content = section.Text, ContentHash = hash, Embedding = Literal(vector),
                });
                embedded++;
            }
        }

        return embedded;
    }

    public async Task<IReadOnlyList<RunbookHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        var vector = await embeddings.EmbedAsync(query, cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var semantic = (await connection.QueryAsync<(string RunbookId, string Section, double Similarity)>(
            new CommandDefinition(Sql.Get("Runbooks.VectorSearch"), new { Embedding = Literal(vector), Limit = 12 }, cancellationToken: cancellationToken))).ToList();
        var lexical = (await connection.QueryAsync<(string RunbookId, string Section, float Rank)>(
            new CommandDefinition(Sql.Get("Runbooks.TextSearch"), new { Query = query, Limit = 12 }, cancellationToken: cancellationToken))).ToList();

        var fused = ReciprocalRankFusion.Fuse([[.. semantic.Select(s => Key(s.RunbookId, s.Section))], [.. lexical.Select(l => Key(l.RunbookId, l.Section))]]);
        var relevant = new HashSet<string>(
            lexical.Select(l => Key(l.RunbookId, l.Section)).Concat(semantic.Where(s => s.Similarity >= minimumSimilarity).Select(s => Key(s.RunbookId, s.Section))),
            StringComparer.Ordinal);

        var byRunbook = fused.Where(f => relevant.Contains(f.Key))
            .Select(f => (RunbookId: f.Key.Split('#', 2)[0], Section: f.Key.Split('#', 2)[1], f.Score))
            .GroupBy(f => f.RunbookId, StringComparer.Ordinal)
            .Select(g => (RunbookId: g.Key, Score: g.Sum(x => x.Score), Sections: g.Select(x => x.Section).ToList()))
            .OrderByDescending(g => g.Score)
            .Take(limit)
            .ToList();
        if (byRunbook.Count == 0)
        {
            return [];
        }

        var documents = (await connection.QueryAsync<(string RunbookId, string Title, string Markdown, string[] Sections)>(
            new CommandDefinition(Sql.Get("Runbooks.Get"), new { RunbookIds = byRunbook.Select(b => b.RunbookId).ToArray() }, cancellationToken: cancellationToken)))
            .ToDictionary(d => d.RunbookId, StringComparer.Ordinal);
        return [.. byRunbook.Where(b => documents.ContainsKey(b.RunbookId)).Select(b =>
            new RunbookHit(b.RunbookId, documents[b.RunbookId].Title, b.Sections, documents[b.RunbookId].Sections, documents[b.RunbookId].Markdown, b.Score))];
    }

    private static string Key(string runbookId, string section) => $"{runbookId}#{section}";

    private static string Literal(float[] vector) => "[" + string.Join(',', vector.Select(v => v.ToString("G7", CultureInfo.InvariantCulture))) + "]";
}
