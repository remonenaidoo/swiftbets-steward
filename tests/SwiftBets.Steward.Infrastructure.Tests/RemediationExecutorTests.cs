using System.Net;
using SwiftBets.Steward.Domain.Remediation;
using SwiftBets.Steward.Infrastructure.Platform;

namespace SwiftBets.Steward.Infrastructure.Tests;

public sealed class RemediationExecutorTests
{
    [Fact]
    public async Task Engaging_the_kill_switch_sets_it_at_config_with_the_action_as_its_reason()
    {
        var calls = new Calls();
        var action = RemediationAction.Propose(Guid.NewGuid(), ActionType.EngageKillSwitch, "placement", "ledger drift", DateTimeOffset.UtcNow);

        var (succeeded, _) = await new HttpRemediationExecutor(calls).ExecuteAsync(action, TestContext.Current.CancellationToken);

        succeeded.ShouldBeTrue();
        var (client, method, path, body) = calls.Seen.ShouldHaveSingleItem();
        (client, method, path).ShouldBe((HttpPlatformInspector.Config, "PUT", "/admin/config/placement.kill-switch"));
        body.ShouldContain("\"value\":\"on\"");
        body.ShouldContain(action.ActionId.ToString());
    }

    [Fact]
    public async Task A_redrive_with_any_target_but_all_is_refused_without_a_call()
    {
        var calls = new Calls();
        var action = RemediationAction.Propose(Guid.NewGuid(), ActionType.RedrivePayouts, "some-coupon", "outage over", DateTimeOffset.UtcNow);

        var (succeeded, _) = await new HttpRemediationExecutor(calls).ExecuteAsync(action, TestContext.Current.CancellationToken);

        succeeded.ShouldBeFalse();
        calls.Seen.ShouldBeEmpty();
    }

    private sealed class Calls : IHttpClientFactory
    {
        public List<(string Client, string Method, string Path, string Body)> Seen { get; } = [];

        public HttpClient CreateClient(string name) => new(new Recorder(this, name)) { BaseAddress = new Uri($"http://{name}/") };

        private sealed class Recorder(Calls owner, string name) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                owner.Seen.Add((name, request.Method.Method, request.RequestUri!.AbsolutePath, request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
            }
        }
    }
}
