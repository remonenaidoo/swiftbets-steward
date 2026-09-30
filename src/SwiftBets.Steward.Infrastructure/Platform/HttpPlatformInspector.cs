using System.Net;
using System.Text.Json;
using SwiftBets.Steward.Application.Ports;

namespace SwiftBets.Steward.Infrastructure.Platform;

public sealed class HttpPlatformInspector(IHttpClientFactory clients) : IPlatformInspector
{
    public const string Settlement = "settlement";
    public const string Payout = "payout";
    public const string Prometheus = "prometheus";

    public IReadOnlyList<string> Metrics => [.. MetricCatalog.Queries.Keys];

    public async Task<string> CouponStateAsync(Guid couponId, CancellationToken cancellationToken)
    {
        var settlement = GetJsonAsync(Settlement, $"coupons/{couponId}/state", cancellationToken);
        var payout = GetJsonAsync(Payout, $"coupons/{couponId}/payout", cancellationToken);
        return $$"""{"couponId":"{{couponId}}","settlement":{{await settlement}},"payout":{{await payout}}}""";
    }

    public async Task<string> MetricAsync(string metric, CancellationToken cancellationToken)
    {
        if (!MetricCatalog.Queries.TryGetValue(metric, out var query))
        {
            return JsonSerializer.Serialize(new { error = $"unknown metric {metric}" });
        }

        var body = await GetJsonAsync(Prometheus, $"api/v1/query?query={Uri.EscapeDataString(query)}", cancellationToken);
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("data", out var data))
        {
            return body;
        }

        var series = data.GetProperty("result").EnumerateArray()
            .Select(r => new { labels = r.GetProperty("metric"), value = r.GetProperty("value")[1].GetString() })
            .ToList();
        return JsonSerializer.Serialize(new { metric, series });
    }

    private async Task<string> GetJsonAsync(string client, string path, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await clients.CreateClient(client).GetAsync(new Uri(path, UriKind.Relative), cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return response.StatusCode == HttpStatusCode.OK
                ? body
                : JsonSerializer.Serialize(new { status = (int)response.StatusCode, body = body.Length > 500 ? body[..500] : body });
        }
        catch (HttpRequestException ex)
        {
            return JsonSerializer.Serialize(new { error = $"{client} unreachable: {ex.Message}" });
        }
    }
}
