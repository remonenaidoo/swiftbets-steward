using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Contracts.Settlement;
using SwiftBets.Steward.Application.Ports;

namespace SwiftBets.Steward.Infrastructure.Platform;

/// <summary>
/// The four drills. Two arm fault points inside services (a dropped settle, a wallet outage); two publish directly
/// (a poison result, a re-published settlement). Each is a real failure path, not a simulation of the detector.
/// </summary>
public sealed class FaultDriver(IHttpClientFactory clients, IEventPublisher publisher, IEventLog events, IOptions<KafkaOptions> kafka, TimeProvider time) : IFaultDriver
{
    public const string StuckCoupon = "stuck-coupon";
    public const string WalletOutage = "wallet-outage";
    public const string PoisonMessage = "poison-message";
    public const string DuplicateSettlement = "duplicate-settlement";

    public IReadOnlyList<string> Faults => [StuckCoupon, WalletOutage, PoisonMessage, DuplicateSettlement];

    public async Task<string> InjectAsync(string fault, CancellationToken cancellationToken)
    {
        switch (fault)
        {
            case StuckCoupon:
                return await ArmAsync(HttpPlatformInspector.Settlement, "settlement.settler.drop", 3, cancellationToken);
            case WalletOutage:
                return await ArmForAsync("wallet", "wallet.unavailable", TimeSpan.FromSeconds(90), cancellationToken);
            case PoisonMessage:
                var topic = TopicName.For(Topics.ResultPublished, kafka.Value.Environment).Value;
                await publisher.PublishRawAsync(new OutgoingMessage(topic, $"poison-{Guid.NewGuid():N}"[..20], Encoding.UTF8.GetBytes("{\"result\": \"this is not an envelope\""), new Dictionary<string, string>()), cancellationToken);
                return $"published a malformed message to {topic}";
            case DuplicateSettlement:
                var settled = (await events.RecentAsync(Topics.CouponSettledV2, 20, cancellationToken)).FirstOrDefault(e => e.EventType == CouponSettledV2.EventType);
                if (settled is null)
                {
                    return "no recent settlement to duplicate yet; place some bets and retry";
                }

                var payload = JsonSerializer.Deserialize<CouponSettledV2>(settled.PayloadJson, SwiftBets.Contracts.Serialization.ContractJson.Options)!;
                await publisher.PublishAsync(Topics.CouponSettledV2, payload.CouponId.ToString(), EventEnvelope<CouponSettledV2>.Create(payload, time.GetUtcNow(), $"drill-{Guid.NewGuid():N}"), cancellationToken);
                return $"re-published settlement v{payload.SettlementVersion} of coupon {payload.CouponId} with a new event id";
            default:
                throw new ArgumentOutOfRangeException(nameof(fault), fault, "Unknown fault.");
        }
    }

    private async Task<string> ArmForAsync(string client, string point, TimeSpan duration, CancellationToken cancellationToken)
    {
        using var response = await clients.CreateClient(client).PostAsync(new Uri($"faults/{point}?seconds={(int)duration.TotalSeconds}", UriKind.Relative), null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return $"armed {point} on {client} for {(int)duration.TotalSeconds} seconds";
    }

    private async Task<string> ArmAsync(string client, string point, int times, CancellationToken cancellationToken)
    {
        using var response = await clients.CreateClient(client).PostAsync(new Uri($"faults/{point}?times={times}", UriKind.Relative), null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return $"armed {point} on {client} for the next {times} hits";
    }
}
