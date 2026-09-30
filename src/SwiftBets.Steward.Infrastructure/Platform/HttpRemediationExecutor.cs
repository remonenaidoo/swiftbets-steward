using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Domain.Remediation;

namespace SwiftBets.Steward.Infrastructure.Platform;

/// <summary>Carries out an approved remediation against the owning service; the action id travels as the idempotency key.</summary>
public sealed class HttpRemediationExecutor(IHttpClientFactory clients) : IRemediationExecutor
{
    public async Task<(bool Succeeded, string Outcome)> ExecuteAsync(RemediationAction action, CancellationToken cancellationToken)
    {
        var (client, path) = action.Type switch
        {
            ActionType.RefreshCoupon when Guid.TryParse(action.Target, out var coupon) => (HttpPlatformInspector.Settlement, $"coupons/{coupon}/refresh"),
            ActionType.SuspendMarket when action.Target.Split('/') is [var fixture, var market] => ("offer", $"fixtures/{fixture}/markets/{market}/suspend"),
            ActionType.ReplayDeadLetter when action.Target.Split('/') is [var coupon, var version] && Guid.TryParse(coupon, out _) && int.TryParse(version, out _) => (HttpPlatformInspector.Payout, $"dead-letters/{coupon}/{version}/replay"),
            _ => (null, null),
        };
        if (client is null)
        {
            return (false, $"target '{action.Target}' is not valid for {action.Type}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(path!, UriKind.Relative));
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
