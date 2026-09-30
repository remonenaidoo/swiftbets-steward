using SwiftBets.Steward.Domain.Reports;

namespace SwiftBets.Steward.Domain.Tests;

public sealed class ReportValidatorTests
{
    private static readonly ToolCallRecord Call = new("toolu_1", "get_coupon_state", "{}", """{"settlement":{"legCount":2,"redisResolvedLegs":0}}""");
    private static readonly HashSet<string> Retrieved = ["stuck-coupon#Diagnosis"];

    [Fact]
    public void Report_quoting_real_tool_output_and_retrieved_sections_is_accepted() =>
        ReportValidator.Validate(Report("\"redisResolvedLegs\":0"), [Call], Retrieved).ShouldBeEmpty();

    [Fact]
    public void Report_with_a_fabricated_excerpt_is_rejected() =>
        ReportValidator.Validate(Report("\"redisResolvedLegs\":2"), [Call], Retrieved).ShouldContain(p => p.Contains("does not appear in the output of toolu_1"));

    private static IncidentReport Report(string excerpt) =>
        new("Coupon stuck", "medium", "Redis progress lost", "SQL has both legs", 0.8,
            [new EvidenceItem("toolu_1", "Redis shows no progress", excerpt)],
            [new RunbookCitation("stuck-coupon", "Diagnosis")],
            [new ProposedAction("refresh_coupon", "c1", "rebuild from SQL")]);
}
