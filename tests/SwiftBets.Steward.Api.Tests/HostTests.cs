using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SwiftBets.Steward.Api.Tests;

public sealed class HostTests : IClassFixture<HostTests.Factory>
{
    private readonly HttpClient _client;

    public HostTests(Factory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Liveness_is_healthy_without_dependencies()
    {
        using var response = await _client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_reports_unavailable_dependencies()
    {
        using var response = await _client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Unknown_route_returns_the_error_envelope()
    {
        using var response = await _client.GetAsync(new Uri("/no-such-route", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Metrics_are_exposed()
    {
        var body = await _client.GetStringAsync(new Uri("/metrics", UriKind.Relative), TestContext.Current.CancellationToken);

        body.ShouldContain("process_cpu_seconds_total");
    }

    [Fact]
    public async Task Alertmanager_without_the_shared_token_is_refused()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/alerts/alertmanager", UriKind.Relative))
        {
            Content = new StringContent("""{"status":"firing","alerts":[{"status":"firing","labels":{"alertname":"WalletLedgerDrift"}}]}""", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "wrong");

        using var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
        builder.UseSetting("ConnectionStrings:SbSteward", "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=1");
        builder.UseSetting("Kafka:BootstrapServers", "127.0.0.1:1");
            builder.UseSetting("Jwt:Authority", "https://identity.test");
            builder.UseSetting("Steward:RunDetectors", "false");
            builder.UseSetting("Steward:AlertWebhookToken", "alert-token");
            builder.UseSetting("Steward:ModelProvider:Provider", "disabled");
            builder.UseSetting("ServiceIdentity:TokenEndpoint", "http://127.0.0.1:1/auth/token");
            builder.UseSetting("ServiceIdentity:ClientSecret", "unused");
            builder.UseSetting("Platform:SettlementAddress", "http://127.0.0.1:1");
            builder.UseSetting("Platform:PayoutAddress", "http://127.0.0.1:1");
            builder.UseSetting("Platform:OfferAddress", "http://127.0.0.1:1");
            builder.UseSetting("Platform:WalletAddress", "http://127.0.0.1:1");
            builder.UseSetting("Platform:PrometheusAddress", "http://127.0.0.1:1");
        }
    }
}
