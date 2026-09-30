namespace SwiftBets.Steward.Domain.Runbooks;

/// <summary>Merges ranked lists (vector and full-text) by summing 1 / (k + rank); robust to their incomparable raw scores.</summary>
public static class ReciprocalRankFusion
{
    public const int DefaultK = 60;

    public static IReadOnlyList<(string Key, double Score)> Fuse(IEnumerable<IReadOnlyList<string>> rankings, int k = DefaultK)
    {
        var scores = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var ranking in rankings)
        {
            for (var rank = 0; rank < ranking.Count; rank++)
            {
                scores[ranking[rank]] = scores.GetValueOrDefault(ranking[rank]) + (1.0 / (k + rank + 1));
            }
        }

        return [.. scores.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, p.Value))];
    }
}
