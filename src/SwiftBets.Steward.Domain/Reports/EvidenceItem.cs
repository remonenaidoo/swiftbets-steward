namespace SwiftBets.Steward.Domain.Reports;

/// <summary><paramref name="Excerpt"/> must appear verbatim in the output of the tool call it names.</summary>
public sealed record EvidenceItem(string ToolCallId, string Claim, string Excerpt);
