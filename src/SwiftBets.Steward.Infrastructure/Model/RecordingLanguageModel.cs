using System.Collections.Concurrent;
using System.Text.Json;
using SwiftBets.Steward.Application.Model;
using SwiftBets.Steward.Application.Ports;

namespace SwiftBets.Steward.Infrastructure.Model;

/// <summary>Wraps a live model and writes each scenario's turns to disk, producing the transcripts the replay model plays.</summary>
public sealed class RecordingLanguageModel(ILanguageModel inner, string directory) : ILanguageModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
    private readonly ConcurrentDictionary<string, List<ModelTurn>> _turns = new(StringComparer.Ordinal);

    public async Task<ModelTurn> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        var turn = await inner.CompleteAsync(request, cancellationToken);
        var turns = _turns.GetOrAdd(request.Scenario, _ => []);
        lock (turns)
        {
            var index = request.Messages.Count(m => m.IsAssistant);
            if (index == 0)
            {
                turns.Clear();
            }

            turns.Add(turn);
        }

        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, $"{request.Scenario}.json"), JsonSerializer.Serialize(new ReplayLanguageModel.Transcript(request.Scenario, [.. turns]), Json), cancellationToken);
        return turn;
    }
}
