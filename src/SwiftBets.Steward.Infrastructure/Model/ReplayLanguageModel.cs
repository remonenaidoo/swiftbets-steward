using System.Text.Json;
using SwiftBets.Steward.Application.Model;
using SwiftBets.Steward.Application.Ports;

namespace SwiftBets.Steward.Infrastructure.Model;

/// <summary>
/// Plays back recorded model turns (one transcript per incident kind, turn N for the Nth assistant turn). CI and
/// local runs without an API key use this: the whole pipeline around the model is exercised, at no cost.
/// </summary>
public sealed class ReplayLanguageModel(string directory) : ILanguageModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ModelTurn> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, $"{request.Scenario}.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"No recorded transcript for scenario {request.Scenario} at {path}.");
        }

        var transcript = JsonSerializer.Deserialize<Transcript>(await File.ReadAllTextAsync(path, cancellationToken), Json)!;
        var turn = request.Messages.Count(m => m.IsAssistant);
        return turn < transcript.Turns.Count
            ? transcript.Turns[turn]
            : throw new InvalidOperationException($"Transcript {request.Scenario} has {transcript.Turns.Count} turns; turn {turn + 1} was requested.");
    }

    public sealed record Transcript(string Scenario, IReadOnlyList<ModelTurn> Turns);
}
