using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwiftBets.Steward.Application.Detection;
using SwiftBets.Steward.Infrastructure.Platform;

namespace SwiftBets.Steward.Infrastructure.Workers;

/// <summary>Polls the payout ladder's retry rate; a sustained burst means the wallet is refusing or unreachable.</summary>
public sealed partial class WalletOutageProbe(IHttpClientFactory clients, IServiceScopeFactory scopes, TimeProvider time, ILogger<WalletOutageProbe> logger) : BackgroundService
{
    private const string Query = "sum(increase(swiftbets_payout_retries_scheduled_total[1m]))";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var body = await clients.CreateClient(HttpPlatformInspector.Prometheus).GetStringAsync(new Uri($"api/v1/query?query={Uri.EscapeDataString(Query)}", UriKind.Relative), stoppingToken);
                using var document = JsonDocument.Parse(body);
                var result = document.RootElement.GetProperty("data").GetProperty("result");
                var rate = result.GetArrayLength() == 0 ? 0 : double.Parse(result[0].GetProperty("value")[1].GetString()!, CultureInfo.InvariantCulture);
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<DetectionRules>().OnLadderRateAsync(rate, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                LogProbeFailed(ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Wallet outage probe failed")]
    private partial void LogProbeFailed(Exception exception);
}
