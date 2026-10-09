using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class ProjectApprovalReviewTests
{
    [Theory]
    [InlineData("Approve")]
    [InlineData("RequestRevision")]
    [InlineData("Escalate")]
    public async Task ManagerAnalyzesCurrentCommandAndSpendingThenRecordsExactBoundDecision(string verdict)
    {
        var manager = Guid.NewGuid(); var producer = Guid.NewGuid(); var proposal = Guid.NewGuid();
        var pending = true; var models = 0; JsonElement decision = default;
        var plan = new WorkstreamPlanProposalV2Request("Game", "Accepted prototype", ["Playable"], "Concept", producer, null, [], [],
            null, null, null, null, "Deliver", "proposal-key", "game", 1, JsonSerializer.SerializeToElement(new { }),
            new(null, 14, [], ["launch"], ["work-planning"], null), [], []);
        var cached = new Dictionary<string, AgentOperatingStateResponse>();
        var runtime = new AgentTestRuntime()
            .RegisterCapability<JsonElement, JsonElement>("platform.project-approval.read.v1", (_, _) => Task.FromResult(JsonSerializer.SerializeToElement(
                pending ? new object[] { new { proposalId = proposal, status = "Pending", requesterId = producer, approverId = manager,
                    binding = new { payload = plan, payloadHash = "exact-hash", idempotencyKey = "proposal-key" },
                    spending = new { mode = "Unlimited", maximumAmount = (decimal?)null, currency = (string?)null } } } : [])))
            .RegisterCapability<AgentOperatingStateReadRequest, AgentOperatingStateReadResponse>(PlatformCapabilities.AgentOperatingStateRead,
                (r, _) => Task.FromResult(new AgentOperatingStateReadResponse(cached.GetValueOrDefault(r.StateKey))))
            .RegisterCapability<AgentOperatingStateWriteRequest, AgentOperatingStateResponse>(PlatformCapabilities.AgentOperatingStateWrite,
                (r, _) => Task.FromResult(cached[r.StateKey] = new(Guid.NewGuid(), r.StateKey, r.SchemaId, r.SchemaVersion, r.Status,
                    r.SourceRevisions, r.ConditionCodes, r.DecisionFingerprint, r.OpenCommitmentCorrelations, r.AttentionReviewId, r.Payload, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)))
            .RegisterCapability<JsonElement, JsonElement>(PlatformCapabilities.LlmChatStream, (r, _) => {
                models++;
                var prompt = r.GetProperty("messages")[0].GetProperty("text").GetString()!;
                Assert.Contains("spending policy", prompt); Assert.Contains("Escalate", prompt);
                return Task.FromResult(JsonSerializer.SerializeToElement(new { text = JsonSerializer.Serialize(new { decisionKind = verdict, rationale = "Specific review assessment." }), role = "assistant" })); })
            .RegisterCapability<JsonElement, JsonElement>("platform.project-approval.decide.v1", (r, _) => {
                decision = r; pending = false; return Task.FromResult(JsonSerializer.SerializeToElement(Array.Empty<object>())); });
        var context = runtime.CreateContext(identity: new(manager.ToString(), "Naomi", null, "Creative Director", null, [], null, Guid.NewGuid().ToString(), "Owner"));
        var agent = new VideoGameCreativeDirectorAgent();
        var configured = await agent.ExecuteCapabilityAsync(new(Guid.NewGuid(), AgentConfigurationCapabilities.Update,
            JsonSerializer.SerializeToElement(new UpdateAgentConfigurationRequest(new Dictionary<string, JsonElement> {
                ["llmProviderId"] = JsonSerializer.SerializeToElement(Guid.NewGuid()), ["llmModel"] = JsonSerializer.SerializeToElement("test") }))), context, default);
        Assert.True(configured.Succeeded, configured.Error);
        await agent.ReviewAssignedProjectsAsync(context, default);
        await agent.ReviewAssignedProjectsAsync(context, default);
        Assert.Equal(1, models);
        Assert.Equal(proposal, decision.GetProperty("proposalId").GetGuid());
        Assert.Equal("exact-hash", decision.GetProperty("payloadHash").GetString());
        Assert.Equal("proposal-key", decision.GetProperty("actionIdempotencyKey").GetString());
        Assert.Equal(verdict, decision.GetProperty("decisionKind").GetString());
    }
}
