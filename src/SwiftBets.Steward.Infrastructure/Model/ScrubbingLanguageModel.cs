using System.Text.RegularExpressions;
using SwiftBets.Steward.Application.Model;
using SwiftBets.Steward.Application.Ports;

namespace SwiftBets.Steward.Infrastructure.Model;

/// <summary>
/// Removes personal data from everything sent to the model: email addresses, phone numbers, card numbers and South
/// African ID numbers become placeholders. Coupon, fixture and event ids (UUIDs) are left alone, because diagnosis needs them.
/// </summary>
public sealed partial class ScrubbingLanguageModel(ILanguageModel inner) : ILanguageModel
{
    public Task<ModelTurn> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scrubbed = request with
        {
            Messages = [.. request.Messages.Select(m => m with { Blocks = [.. m.Blocks.Select(b => b with { Text = Scrub(b.Text), InputJson = Scrub(b.InputJson) })] })],
        };
        return inner.CompleteAsync(scrubbed, cancellationToken);
    }

    public static string? Scrub(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        text = Email().Replace(text, "[email]");
        text = Uuid().Replace(text, m => m.Value.Replace('-', '‐'));
        text = LongNumber().Replace(text, "[number]");
        text = Phone().Replace(text, "[phone]");
        return text.Replace('‐', '-');
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex Email();

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b")]
    private static partial Regex Uuid();

    // Card numbers (13 to 19 digits, spaces or dashes allowed) and 13-digit ID numbers.
    [GeneratedRegex(@"\b(?:\d[ -]?){12,18}\d\b")]
    private static partial Regex LongNumber();

    [GeneratedRegex(@"(?:\+27|\b0)[ -]?\d{2}[ -]?\d{3}[ -]?\d{4}\b")]
    private static partial Regex Phone();
}
