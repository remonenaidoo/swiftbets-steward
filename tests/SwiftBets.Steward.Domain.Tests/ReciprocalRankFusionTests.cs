using SwiftBets.Steward.Domain.Runbooks;

namespace SwiftBets.Steward.Domain.Tests;

public sealed class ReciprocalRankFusionTests
{
    [Fact]
    public void Item_ranked_well_by_both_lists_wins() =>
        ReciprocalRankFusion.Fuse([["a", "b", "c"], ["b", "a", "d"]]).Select(f => f.Key).Take(2).ShouldBe(["a", "b"], ignoreOrder: true);

    [Fact]
    public void Nothing_ranked_fuses_to_nothing() => ReciprocalRankFusion.Fuse([[], []]).ShouldBeEmpty();
}
