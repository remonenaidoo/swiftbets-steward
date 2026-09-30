namespace SwiftBets.Steward.Application.Model;

public sealed record ModelTurn(string Model, IReadOnlyList<ModelBlock> Blocks, string StopReason, ModelUsage Usage);
