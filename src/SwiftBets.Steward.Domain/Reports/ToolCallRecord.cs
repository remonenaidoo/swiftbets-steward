namespace SwiftBets.Steward.Domain.Reports;

/// <summary>A tool the agent called during diagnosis and exactly what it returned; the only material a report may cite.</summary>
public sealed record ToolCallRecord(string ToolCallId, string Name, string InputJson, string OutputJson);
