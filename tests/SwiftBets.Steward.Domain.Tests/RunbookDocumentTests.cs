using SwiftBets.Steward.Domain.Runbooks;

namespace SwiftBets.Steward.Domain.Tests;

public sealed class RunbookDocumentTests
{
    [Fact]
    public void Runbook_is_split_into_its_sections()
    {
        var document = RunbookDocument.Parse("---\nid: stuck-coupon\ntitle: Stuck coupon\n---\n# Stuck coupon\n\n## Symptoms\n- a\n\n## Remediation\n- b\n");

        document.RunbookId.ShouldBe("stuck-coupon");
        document.Sections.Select(s => s.Heading).ShouldBe(["Symptoms", "Remediation"]);
    }

    [Fact]
    public void Runbook_without_front_matter_is_rejected() =>
        Should.Throw<FormatException>(() => RunbookDocument.Parse("# Stuck coupon\n## Symptoms\n"));
}
