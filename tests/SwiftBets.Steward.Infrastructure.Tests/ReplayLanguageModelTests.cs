using SwiftBets.Steward.Application.Model;
using SwiftBets.Steward.Infrastructure.Model;

namespace SwiftBets.Steward.Infrastructure.Tests;

public sealed class ReplayLanguageModelTests
{
    private static readonly string Transcripts = Path.Combine(AppContext.BaseDirectory, "transcripts");

    [Fact]
    public async Task Scripted_turn_asks_about_the_live_incident_subject()
    {
        var turn = await new ReplayLanguageModel(Transcripts).CompleteAsync(Request("StuckCoupon", "0199aaaa-0000-7000-8000-000000000001"), TestContext.Current.CancellationToken);

        turn.Blocks.ShouldHaveSingleItem().InputJson.ShouldBe("""{"coupon_id": "0199aaaa-0000-7000-8000-000000000001"}""");
    }

    [Fact]
    public async Task Scenario_without_a_transcript_is_refused() =>
        await Should.ThrowAsync<InvalidOperationException>(() =>
            new ReplayLanguageModel(Transcripts).CompleteAsync(Request("NoSuchKind", "x"), TestContext.Current.CancellationToken));

    private static ModelRequest Request(string scenario, string subject) =>
        new(scenario, "system", [], [new ModelMessage(false, [ModelBlock.FromText($"Incident 1\nKind: {scenario}\nSubject: {subject}\nDetector summary: s")])], 1000);
}
