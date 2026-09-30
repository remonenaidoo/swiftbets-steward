using System.Net.Http.Json;

namespace SwiftBets.Steward.Infrastructure.Runbooks;

/// <summary>Local embeddings from an Ollama server (nomic-embed-text, 768 dimensions); no per-call cost.</summary>
public sealed class OllamaEmbeddingGenerator(HttpClient http, string model) : IEmbeddingGenerator
{
    public int Dimensions => 768;

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("api/embeddings", new { model, prompt = text }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<EmbeddingBody>(cancellationToken) ?? throw new InvalidOperationException("Empty embedding response.");
        return body.Embedding.Length == Dimensions ? body.Embedding : throw new InvalidOperationException($"Expected {Dimensions} dimensions, got {body.Embedding.Length}.");
    }

    private sealed record EmbeddingBody(float[] Embedding);
}
