namespace SwiftBets.Steward.Application.Ports;

public sealed record RunbookHit(string RunbookId, string Title, IReadOnlyList<string> MatchedSections, IReadOnlyList<string> AllSections, string Markdown, double Score);
