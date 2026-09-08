using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class ProductionCommitmentTests
{
    [Fact]
    public async Task PlanningQueuesIndependentStableTasksWithoutExecutingDecisionsOrRequeueingBlockers()
    {
        var state = State();
        var tasks = new Dictionary<string, PersonalTodoItem>();
        var runtime = new AgentTestRuntime().RegisterCapability<AddPersonalTodoItemRequest, PersonalTodoItem>(
            PersonalTodoCapabilities.Add, (r, _) => {
                if (!tasks.TryGetValue(r.IdempotencyKey, out var task))
                    tasks[r.IdempotencyKey] = task = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Director",
                        r.Title, r.Description!, "Blocked", r.Priority, 1, 1, null, r.SourceConversationId, r.SourceMessageId,
                        [], null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow) { CorrelationId = r.CorrelationId, WorkContext = r.WorkContext };
                return Task.FromResult(task);
            });
        var context = runtime.CreateContext();
        await VideoGameCreativeDirectorAgent.QueueProductionCommitmentsAsync(state, context, default);
        await VideoGameCreativeDirectorAgent.QueueProductionCommitmentsAsync(state, context, default);
        Assert.Equal(2, tasks.Count);
        Assert.Contains(tasks.Values, x => x.Title.Contains("asset"));
        Assert.Contains(tasks.Values, x => x.Title.Contains("toolchain"));
        Assert.All(tasks.Values, x => { Assert.Equal("Blocked", x.Status); Assert.Equal(state.WorkstreamId, x.WorkContext!.WorkstreamId); Assert.Equal("Medium", x.Priority); });
    }

    [Fact]
    public async Task ProposedAuthorityCoversTheActualAssetDecisionAndToolchainSelection()
    {
        WorkstreamPlanProposalV2Request? plan = null;
        DecisionRequest? decision = null;
        var runtime = new AgentTestRuntime()
            .RegisterCapability<WorkstreamPlanProposalV2Request, JsonElement>("platform.workstream.plan.propose.v2", (r, _) => {
                plan = r; return Task.FromException<JsonElement>(new InvalidOperationException("captured")); })
            .RegisterCapability<DecisionRequest, DecisionRecord>(DecisionCapabilityNames.RequestV1, (r, _) => {
                decision = r; return Task.FromException<DecisionRecord>(new InvalidOperationException("captured")); });
        var context = runtime.CreateContext(identity: new AgentIdentity(Guid.NewGuid().ToString(), "Director", null, "Creative Director", null, [], null, Guid.NewGuid().ToString(), "Owner"));
        var agent = new VideoGameCreativeDirectorAgent(); var state = State();
        await Assert.ThrowsAnyAsync<Exception>(() => agent.EnsureProjectFoundationAsync(state with { WorkstreamId = null }, 1,
            state.TeamId, Guid.NewGuid(), Guid.NewGuid(), context, default));
        await Assert.ThrowsAnyAsync<Exception>(() => agent.EnsureAssetStrategyAsync(state, 1, Guid.NewGuid(), context, default));
        Assert.NotNull(plan); Assert.NotNull(decision);
        Assert.Contains(decision.AuthorityRuleKey, plan.AuthorityEnvelope.AgentAuthorizedActionKeys);
        Assert.Contains(VideoGameCreativeDirectorAgent.CertifiedToolchainAuthority, plan.AuthorityEnvelope.AgentAuthorizedActionKeys);
        Assert.Contains("launch", plan.AuthorityEnvelope.HumanRequiredActionKeys);
        Assert.DoesNotContain("launch", plan.AuthorityEnvelope.AgentAuthorizedActionKeys);
    }

    [Fact]
    public async Task AuthorizedResolvedDecisionIsConsumedWithoutASecondApproval()
    {
        var decision = new DecisionRecord(Guid.NewGuid(), Guid.NewGuid(), "video-game.asset-strategy.v1", "Asset strategy",
            VideoGameCreativeDirectorAgent.ProductionStrategyAuthority, [], "procedural", "procedural", "Decided", "Approved",
            [], null, null, null, 2, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var context = new AgentTestRuntime().CreateContext();
        Assert.Equal(decision, await VideoGameCreativeDirectorAgent.DecideProductionChoiceAsync(decision,
            "procedural", "Already approved", "replay", context, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => VideoGameCreativeDirectorAgent.DecideProductionChoiceAsync(
            decision, "hybrid", "Different choice", "replay-other", context, default));
    }

    private static CreativeDirectorOperatingState State() => new() {
        WorkstreamId = Guid.NewGuid(), TeamId = Guid.NewGuid(), WorkingTitle = "Gridlock",
        AcceptedVision = new(1, "digest", "# Gridlock\nA local web arena.", Guid.NewGuid(), Guid.NewGuid(), "hash",
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow)
    };
}
