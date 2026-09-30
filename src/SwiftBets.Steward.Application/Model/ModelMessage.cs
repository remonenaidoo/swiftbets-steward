namespace SwiftBets.Steward.Application.Model;

public sealed record ModelMessage(bool IsAssistant, IReadOnlyList<ModelBlock> Blocks);
