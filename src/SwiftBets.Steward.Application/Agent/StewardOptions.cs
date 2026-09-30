using System.ComponentModel.DataAnnotations;

namespace SwiftBets.Steward.Application.Agent;

public sealed class StewardOptions
{
    public const string SectionName = "Steward";

    [Required]
    public string Model { get; set; } = "claude-sonnet-5-5";

    [Range(1, 20)]
    public int MaxTurns { get; set; } = 8;

    [Range(256, 64_000)]
    public int MaxTokens { get; set; } = 8_000;

    /// <summary>Hard monthly ceiling; diagnosis refuses to call the model once spend reaches it.</summary>
    [Range(0, 10_000)]
    public decimal MonthlyBudgetUsd { get; set; } = 10m;

    /// <summary>USD per million tokens: input, output, cache write, cache read.</summary>
    public Dictionary<string, decimal[]> Pricing { get; set; } = new(StringComparer.Ordinal);
}
