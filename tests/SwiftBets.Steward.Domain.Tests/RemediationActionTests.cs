using SwiftBets.Steward.Domain.Remediation;

namespace SwiftBets.Steward.Domain.Tests;

public sealed class RemediationActionTests
{
    [Fact]
    public void Pending_action_can_be_approved_by_an_operator()
    {
        var approved = Pending().Approve("operator1", DateTimeOffset.UtcNow);

        approved!.Status.ShouldBe(ActionStatus.Approved);
        approved.DecidedBy.ShouldBe("operator1");
    }

    [Fact]
    public void Rejected_action_can_never_be_approved() =>
        Pending().Reject("operator1", DateTimeOffset.UtcNow)!.Approve("operator2", DateTimeOffset.UtcNow).ShouldBeNull();

    private static RemediationAction Pending() => RemediationAction.Propose(Guid.NewGuid(), ActionType.RefreshCoupon, "c1", "rebuild", DateTimeOffset.UtcNow);
}
