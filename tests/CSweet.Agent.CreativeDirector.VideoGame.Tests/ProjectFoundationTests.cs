using CSweet.Agent.SDK;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class ProjectFoundationTests
{
    [Fact]
    public async Task ApprovedProjectCanHandoffWithoutBoardCreationAuthority()
    {
        var team = Guid.NewGuid(); var producer = Guid.NewGuid();
        var state = new CreativeDirectorOperatingState {
            WorkstreamId = Guid.NewGuid(), TeamId = team, WorkingTitle = "Grid Blast",
            AcceptedVision = new(1, "digest", "# Grid Blast", Guid.NewGuid(), Guid.NewGuid(), "hash",
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow)
        };
        // No board capability is registered: the Director is a supervisor, not the board manager.
        var context = new AgentTestRuntime().CreateContext(identity: new AgentIdentity(Guid.NewGuid().ToString(),
            "Director", null, "Creative Director", null, [], null, Guid.NewGuid().ToString(), "Owner"));
        var result = await new VideoGameCreativeDirectorAgent().EnsureProjectFoundationAsync(state, 1, team,
            producer, Guid.NewGuid(), context, default);
        Assert.True(result.Ready);
        Assert.Null(result.State.BoardId);
    }
}
