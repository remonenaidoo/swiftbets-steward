using System.IO.Hashing;
using System.Text;
using System.Text.RegularExpressions;

namespace SwiftBets.Steward.Infrastructure.Runbooks;

/// <summary>
/// Deterministic bag-of-words embeddings by feature hashing: no model, no network. Used in CI and as the fallback
/// when no embedding model is configured; retrieval quality then leans on the full-text half of the hybrid search.
/// </summary>
public sealed partial class HashingEmbeddingGenerator : IEmbeddingGenerator
{
    public int Dimensions => 768;

    public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        var vector = new float[Dimensions];
        foreach (Match word in Words().Matches(text.ToLowerInvariant()))
        {
            var hash = XxHash32.HashToUInt32(Encoding.UTF8.GetBytes(word.Value));
            vector[hash % Dimensions] += (hash & 0x80000000) == 0 ? 1f : -1f;
        }

        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        if (norm > 0)
        {
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] /= norm;
            }
        }

        return Task.FromResult(vector);
    }

    [GeneratedRegex("[a-z0-9][a-z0-9_-]{2,}")]
    private static partial Regex Words();
}
