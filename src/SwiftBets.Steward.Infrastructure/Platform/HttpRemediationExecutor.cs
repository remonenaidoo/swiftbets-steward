using System.Net.Http.Json;
using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Domain.Remediation;

namespace SwiftBets.Steward.Infrastructure.Platform;

/// <summary>Carries out an approved remediation against the owning service; the action id travels as the idempotency key.</summary>
public sealed class HttpRemediationExecutor(IHttpClientFactory clients) : IRemediationExecutor
{
    public async Task<(bool Succeeded, string Outcome)> ExecuteAsync(RemediationAction action, CancellationToken cancellationToken)
    {
        var (client, method, path, payload) = action.Type switch
        {
            ActionType.RefreshCoupon when Guid.TryParse(action.Target, out var coupon) => (HttpPlatformInspector.Settlement, HttpMethod.Post, $"coupons/{coupon}/refresh", (object?)null),
            ActionType.SuspendMarket when action.Target.Split('/') is [var fixture, var market] => ("offer", HttpMethod.Post, $"fixtures/{fixture}/markets/{market}/suspend", null),
            ActionType.ReplayDeadLetter when action.Target.Split('/') is [var coupon, var version] && Guid.TryParse(coupon, out _) && int.TryParse(version, out _) => (HttpPlatformInspector.Payout, HttpMethod.Post, $"dead-letters/{coupon}/{version}/replay", null),
            ActionType.EngageKillSwitch when action.Target == "placement" => (HttpPlatformInspector.Config, HttpMethod.Put, "admin/config/placement.kill-switch", new { value = "on", reason = $"Steward remediation {action.ActionId}: {action.Rationale}" }),
            ActionType.ReplayPaymentWebhooks => (HttpPlatformInspector.Payments, HttpMethod.Post, "admin/payments/open/sweep", null),
            ActionType.RedrivePayouts when action.Target == "all" => (HttpPlatformInspector.Payout, HttpMethod.Post, "dead-letters/redrive", null),
            _ => ((string?)null, HttpMethod.Post, (string?)null, (object?)null),
        };
        if (client is null)
        {
            return (false, $"target '{action.Target}' is not valid for {action.Type}");
        }

        using var request = new HttpRequestMessage(method, new Uri(path!, UriKind.Relative)) { Content = payload is null ? null : JsonContent.Create(payload) };
        request.Headers.Add("Idempotency-Key", action.ActionId.ToString());
        try
        {
            using var response = await clients.CreateClient(client).SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return (response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {(body.Length > 300 ? body[..300] : body)}");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"{client} unreachable: {ex.Message}");
        }
    }
}
