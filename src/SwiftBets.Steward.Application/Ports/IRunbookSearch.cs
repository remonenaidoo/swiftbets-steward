namespace SwiftBets.Steward.Application.Ports;

public interface IRunbookSearch
{
    /// <summary>Hybrid retrieval over runbook sections; returns whole parent runbooks above the relevance floor.</summary>
    Task<IReadOnlyList<RunbookHit>> SearchAsync(string query, int limit, CancellationToken cancellationToken);
}
