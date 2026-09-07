using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class ProjectFoundationTests
{
    [Fact]
    public async Task FoundationCreatesValidBoardAndRetriesInterruptedPlanningCards()
    {
        var project = Guid.NewGuid(); var team = Guid.NewGuid(); var producer = Guid.NewGuid();
        var board = Guid.NewGuid(); var creates = 0; var failOnce = true;
        var cards = new Dictionary<string, CreateWorkItemRequest>();
        var stored = new Dictionary<string, AgentOperatingStateResponse>();
        var state = new CreativeDirectorOperatingState {
            WorkstreamId = project, TeamId = team, WorkingTitle = "Grid Blast",
            AcceptedVision = new(1, "digest", "# Grid Blast", Guid.NewGuid(), Guid.NewGuid(), "hash",
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow)
        };
        var runtime = new AgentTestRuntime()
            .RegisterCapability<AgentOperatingStateReadRequest, AgentOperatingStateReadResponse>(PlatformCapabilities.AgentOperatingStateRead,
                (r, _) => Task.FromResult(new AgentOperatingStateReadResponse(stored.GetValueOrDefault(r.StateKey))))
            .RegisterCapability<AgentOperatingStateWriteRequest, AgentOperatingStateResponse>(PlatformCapabilities.AgentOperatingStateWrite,
                (r, _) => {
                    var saved = new AgentOperatingStateResponse(Guid.NewGuid(), r.StateKey, r.SchemaId, r.SchemaVersion,
                        r.Status, r.SourceRevisions, r.ConditionCodes, r.DecisionFingerprint, r.OpenCommitmentCorrelations,
                        r.AttentionReviewId, r.Payload, (stored.GetValueOrDefault(r.StateKey)?.Revision ?? 0) + 1,
                        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
                    stored[r.StateKey] = saved;
                    return Task.FromResult(saved);
                })
            .RegisterCapability<CreateWorkBoardRequest, WorkBoardSummary>(WorkBoardCapabilities.Create, (r, _) => {
                Assert.Matches("^[A-Z][A-Z0-9]{1,11}$", r.Key!);
                Assert.Equal(project, r.WorkstreamId); Assert.Equal(team, r.TeamId);
                creates++;
                return Task.FromResult(new WorkBoardSummary(board, r.Name, r.Description!, false, false, 1, []));
            })
            .RegisterCapability<CreateWorkItemRequest, JsonElement>(WorkItemCapabilities.Create, (r, _) => {
                Assert.Equal(board, r.BoardId); Assert.Equal(producer, r.AccountableOrganizationUserId);
                if (cards.Count == 1 && failOnce) { failOnce = false; throw new InvalidOperationException("interrupted seed"); }
                cards.TryAdd(r.IdempotencyKey, r);
                return Task.FromResult(JsonSerializer.SerializeToElement(new { id = Guid.NewGuid() }));
            });
        var context = runtime.CreateContext(identity: new AgentIdentity(Guid.NewGuid().ToString(), "Director", null,
            "Creative Director", null, [], null, Guid.NewGuid().ToString(), "Owner"));
        var agent = new VideoGameCreativeDirectorAgent();
        await Assert.ThrowsAsync<InvalidOperationException>(() => agent.EnsureProjectFoundationAsync(state, null, team,
            producer, Guid.NewGuid(), context, default));
        var persisted = stored[VideoGameCreativeDirectorAgent.ProjectStateKey(project, null)];
        var resumed = JsonSerializer.Deserialize<CreativeDirectorOperatingState>(persisted.Payload.GetRawText(),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var result = await agent.EnsureProjectFoundationAsync(resumed, persisted.Revision, team, producer,
            Guid.NewGuid(), context, default);
        Assert.True(result.Ready);
        Assert.Equal(1, creates);
        Assert.Equal(3, cards.Count);
        Assert.Contains(cards.Values, x => x.Title.Contains("pre-production plan"));
    }
}
