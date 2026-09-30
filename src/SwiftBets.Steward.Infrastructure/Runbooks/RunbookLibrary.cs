using SwiftBets.Steward.Domain.Runbooks;

namespace SwiftBets.Steward.Infrastructure.Runbooks;

/// <summary>The runbooks shipped with Steward (the repository's runbooks/ folder, embedded at build time).</summary>
public static class RunbookLibrary
{
    public static IReadOnlyList<RunbookDocument> Load()
    {
        var assembly = typeof(RunbookLibrary).Assembly;
        return [.. assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith("runbooks/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(n =>
            {
                using var stream = assembly.GetManifestResourceStream(n)!;
                using var reader = new StreamReader(stream);
                return RunbookDocument.Parse(reader.ReadToEnd());
            })];
    }
}
