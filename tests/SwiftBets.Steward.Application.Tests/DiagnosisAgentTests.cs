using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SwiftBets.Steward.Application.Agent;
using SwiftBets.Steward.Application.Model;
using SwiftBets.Steward.Domain.Incidents;
using SwiftBets.Steward.Domain.Remediation;

namespace SwiftBets.Steward.Application.Tests;

public sealed class DiagnosisAgentTests
{
    private static readonly Guid Coupon = Guid.Parse("0199bbbb-0000-7000-8000-000000000001");

    [Fact]
    public async Task Evidence_backed_report_awaits_approval_with_its_proposed_action()
    {
        var (agent, incidents, incident) = Build(Turns(excerpt: "\"redisResolvedLegs\":0"), spent: 0m);

        (await agent.DiagnoseAsync(incident.IncidentId, CancellationToken.None)).ShouldBe(IncidentStatus.AwaitingApproval);

        incidents.Actions.ShouldHaveSingleItem().Type.ShouldBe(ActionType.RefreshCoupon);
        incidents.Calls.Select(c => c.Name).ShouldBe([ToolCatalog.CouponState, ToolCatalog.SearchRunbooks]);
    }

    [Fact]
    public async Task Report_that_keeps_citing_evidence_it_never_saw_is_stored_as_a_failed_diagnosis()
    {
        var (agent, incidents, incident) = Build(Turns(excerpt: "\"redisResolvedLegs\":2"), spent: 0m);

        (await agent.DiagnoseAsync(incident.IncidentId, CancellationToken.None)).ShouldBe(IncidentStatus.DiagnosisFailed);

        incidents.Actions.ShouldBeEmpty();
        incidents.Saved.Problems.ShouldContain(p => p.Contains("does not appear"));
    }

    [Fact]
    public async Task Exhausted_budget_stops_diagnosis_before_any_model_call()
    {
        var model = new ScriptedModel(Turns("x")[0]);
        var (agent, _, incident) = Build([], spent: 10m, model);

        (await agent.DiagnoseAsync(incident.IncidentId, CancellationToken.None)).ShouldBe(IncidentStatus.DiagnosisFailed);
        model.Calls.ShouldBe(0);
    }

    private static IReadOnlyList<ModelBlock>[] Turns(string excerpt) =>
    [
        [ModelBlock.ToolUse("toolu_1", ToolCatalog.CouponState, $$"""{"coupon_id":"{{Coupon}}"}""")],
        [ModelBlock.ToolUse("toolu_2", ToolCatalog.SearchRunbooks, """{"query":"coupon evaluated but not settled"}""")],
        [ModelBlock.ToolUse("toolu_3", ToolCatalog.SubmitReport, $$"""
            {"summary":"Coupon stuck","severity":"medium","root_cause":"Redis progress lost","hypothesis":"SQL has the evaluations","confidence":0.8,
             "evidence":[{"tool_call_id":"toolu_1","claim":"no Redis progress","excerpt":{{System.Text.Json.JsonSerializer.Serialize(excerpt)}}}],
             "runbook_citations":[{"runbook_id":"stuck-coupon","section":"Diagnosis"}],
             "proposed_actions":[{"type":"refresh_coupon","target":"{{Coupon}}","rationale":"rebuild progress from SQL"}]}
            """)],
    ];

    private static (DiagnosisAgent, MemoryIncidents, Incident) Build(IReadOnlyList<ModelBlock>[] turns, decimal spent, ScriptedModel? model = null)
    {
        var incidents = new MemoryIncidents();
        var incident = Incident.Open(IncidentKind.StuckCoupon, Coupon.ToString(), "stuck", DateTimeOffset.UtcNow);
        incidents.Incidents[incident.IncidentId] = incident;
        var agent = new DiagnosisAgent(model ?? new ScriptedModel(turns), incidents, new NoEvents(), new FixedPlatform(), new OneRunbook(), new Budget(spent),
            Options.Create(new StewardOptions()), TimeProvider.System, NullLogger<DiagnosisAgent>.Instance);
        return (agent, incidents, incident);
    }
}
