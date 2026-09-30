using System.Text.Json;
using System.Text.Json.Serialization;
using SwiftBets.Steward.Application.Model;
using SwiftBets.Steward.Application.Ports;

namespace SwiftBets.Steward.Infrastructure.Model;

/// <summary>
/// Plays back scripted model turns (one transcript per incident kind, turn N for the Nth assistant turn). CI, the
/// E2E suite and keyless demos use this: every tool, the evidence validator and the executor still run for real.
/// Transcripts may say <c>{{subject}}</c>; it becomes the incident's subject (a coupon id, a topic), so the scripted
/// model asks about the live incident and its cited excerpts must still match the live tool output.
/// </summary>
public sealed class ReplayLanguageModel(string directory) : ILanguageModel
{
    private const string SubjectLine = "Subject: ";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async Task<ModelTurn> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, $"{request.Scenario}.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"No recorded transcript for scenario {request.Scenario} at {path}.");
        }

        var transcript = JsonSerializer.Deserialize<Transcript>(await File.ReadAllTextAsync(path, cancellationToken), Json)!;
        var index = request.Messages.Count(m => m.IsAssistant);
        if (index >= transcript.Turns.Count)
        {
            throw new InvalidOperationException($"Transcript {request.Scenario} has {transcript.Turns.Count} turns; turn {index + 1} was requested.");
        }

        var subject = Subject(request);
        var turn = transcript.Turns[index];
        return subject is null ? turn : turn with { Blocks = [.. turn.Blocks.Select(b => Fill(b, subject))] };
    }

    private static string? Subject(ModelRequest request) =>
        request.Messages.FirstOrDefault(m => !m.IsAssistant)?.Blocks.FirstOrDefault(b => b.Kind == ModelBlockKind.Text)?.Text?
            .Split('\n').FirstOrDefault(l => l.StartsWith(SubjectLine, StringComparison.Ordinal))?[SubjectLine.Length..].Trim();

    private static ModelBlock Fill(ModelBlock block, string subject)
    {
        // Inside JSON the subject must be escaped exactly as the serializer would write it.
        var escaped = JsonSerializer.Serialize(subject)[1..^1];
        return block with
        {
            Text = block.Text?.Replace("{{subject}}", subject, StringComparison.Ordinal),
            InputJson = block.InputJson?.Replace("{{subject}}", escaped, StringComparison.Ordinal),
        };
    }

    public sealed record Transcript(string Scenario, IReadOnlyList<ModelTurn> Turns);
}
