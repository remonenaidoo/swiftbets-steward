using System.ComponentModel.DataAnnotations;

namespace SwiftBets.Steward.Infrastructure.Platform;

public sealed class PlatformOptions
{
    public const string SectionName = "Platform";

    [Required][Url] public string SettlementAddress { get; set; } = string.Empty;

    [Required][Url] public string PayoutAddress { get; set; } = string.Empty;

    [Required][Url] public string OfferAddress { get; set; } = string.Empty;

    [Required][Url] public string WalletAddress { get; set; } = string.Empty;

    [Required][Url] public string PrometheusAddress { get; set; } = string.Empty;

    [Url] public string CasinoAddress { get; set; } = "http://casino:8080";

    [Url] public string PaymentsAddress { get; set; } = "http://payments:8080";

    [Url] public string ConfigAddress { get; set; } = "http://config:8080";
}
