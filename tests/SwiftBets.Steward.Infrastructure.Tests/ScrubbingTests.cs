using SwiftBets.Steward.Application.Model;
using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Infrastructure.Model;

namespace SwiftBets.Steward.Infrastructure.Tests;

public sealed class ScrubbingTests
{
    [Fact]
    public async Task Personal_data_in_a_tool_result_never_reaches_the_model()
    {
        var inner = new Capture();
        var result = ModelBlock.ToolResult("toolu_01", """{"email":"thandi@example.co.za","phone":"+27 82 555 1234","card":"4111 1111 1111 1111","id":"9001015009087"}""");

        await new ScrubbingLanguageModel(inner).CompleteAsync(Request(result), TestContext.Current.CancellationToken);

        var sent = inner.Seen!.Messages[0].Blocks[0].Text!;
        sent.ShouldNotContain("thandi@example.co.za");
        sent.ShouldNotContain("555 1234");
        sent.ShouldNotContain("4111 1111");
        sent.ShouldNotContain("9001015009087");
    }

    [Fact]
    public async Task Coupon_and_fixture_ids_pass_through_untouched()
    {
        var inner = new Capture();
        const string text = """{"couponId":"01a1006b-c969-70ab-a1cb-1dd754429b86","fixtureId":"2024-25-c1040-m189","stake":1000}""";

        await new ScrubbingLanguageModel(inner).CompleteAsync(Request(ModelBlock.ToolResult("toolu_01", text)), TestContext.Current.CancellationToken);

        inner.Seen!.Messages[0].Blocks[0].Text.ShouldBe(text);
    }

    private static ModelRequest Request(ModelBlock block) => new("scenario", "system", [], [new ModelMessage(false, [block])], 100);

    private sealed class Capture : ILanguageModel
    {
        public ModelRequest? Seen { get; private set; }

        public Task<ModelTurn> CompleteAsync(ModelRequest request, CancellationToken cancellationToken)
        {
            Seen = request;
            return Task.FromResult(new ModelTurn("test", [], "end_turn", new ModelUsage(0, 0, 0, 0)));
        }
    }
}
