namespace SwiftBets.Steward.Infrastructure.Runbooks;

public interface IEmbeddingGenerator
{
    int Dimensions { get; }

    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken);
}
