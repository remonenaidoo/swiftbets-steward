namespace SwiftBets.Steward.Application.Model;

/// <summary><see cref="System"/> and <see cref="Tools"/> must be byte-stable across calls so the provider's prompt cache hits.</summary>
public sealed record ModelRequest(string Scenario, string System, IReadOnlyList<ToolSpec> Tools, IReadOnlyList<ModelMessage> Messages, int MaxTokens);
