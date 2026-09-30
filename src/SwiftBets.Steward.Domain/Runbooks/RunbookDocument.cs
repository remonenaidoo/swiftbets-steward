namespace SwiftBets.Steward.Domain.Runbooks;

/// <summary>A runbook split into its <c>##</c> sections: sections are what retrieval indexes, the whole document is what is returned.</summary>
public sealed record RunbookDocument(string RunbookId, string Title, string Markdown, IReadOnlyList<RunbookSection> Sections)
{
    public static RunbookDocument Parse(string markdown)
    {
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        if (lines.Length < 3 || lines[0] != "---")
        {
            throw new FormatException("A runbook starts with front matter declaring id and title.");
        }

        var end = Array.IndexOf(lines, "---", 1);
        var front = lines[1..end].Select(l => l.Split(':', 2)).Where(p => p.Length == 2).ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.Ordinal);
        if (!front.TryGetValue("id", out var id) || !front.TryGetValue("title", out var title))
        {
            throw new FormatException("Runbook front matter needs id and title.");
        }

        var sections = new List<RunbookSection>();
        string? heading = null;
        var body = new List<string>();
        foreach (var line in lines[(end + 1)..])
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush();
                heading = line[3..].Trim();
            }
            else if (heading is not null)
            {
                body.Add(line);
            }
        }

        Flush();
        return new RunbookDocument(id, title, string.Join('\n', lines[(end + 1)..]).Trim(), sections);

        void Flush()
        {
            if (heading is not null)
            {
                sections.Add(new RunbookSection(id, heading, string.Join('\n', body).Trim()));
            }

            body.Clear();
        }
    }
}
