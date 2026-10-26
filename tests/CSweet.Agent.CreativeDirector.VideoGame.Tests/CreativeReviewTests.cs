using System.Text.Json;
using CSweet.Agent.SDK;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class CreativeReviewTests
{
    [Theory]
    [InlineData("accepted", false, false, true)]
    [InlineData("accepted", true, false, false)]
    [InlineData("changes-required", false, true, true)]
    [InlineData("changes-required", false, false, false)]
    [InlineData("escalate", true, true, true)]
    [InlineData("escalate", false, true, false)]
    public void Disposition_requires_consistent_findings(string disposition, bool director, bool findings, bool valid)
    {
        var result = new CreativeReviewResult(disposition, "Exact review", findings ? ["Player promise conflict"] : [],
            findings ? ["Resolve with the accountable owner"] : [], director);
        Assert.Equal(valid, VideoGameCreativeDirectorAgent.ValidReview(result));
    }

    [Fact]
    public async Task Exact_review_uses_sdk_independent_mode_and_replays_without_model_or_approval()
    {
        var j = Fixture(); await j.Configure();
        var request = j.ReviewRequest();
        var first = await j.Agent.HandleCoordinationTurnAsync(request, j.Context, default);
        var replay = await j.Agent.HandleCoordinationTurnAsync(request, j.Context, default);
        Assert.Equal("Continue", first.Disposition);
        Assert.Equal(first.Artifact!.Payload.GetRawText(), replay.Artifact!.Payload.GetRawText());
        Assert.Equal(1, j.Models);
        Assert.Contains("independent-reviewer", j.SystemPrompt);
        Assert.Contains("Never require a playable build", j.SystemPrompt);
        Assert.Contains("when reviewing an implemented playable", j.SystemPrompt);
        Assert.Contains("does not", j.SystemPrompt);
        Assert.Empty(j.Documents); // No authored document, CEO action or artifact acceptance grant is available.
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("project")]
    [InlineData("team")]
    [InlineData("hash")]
    [InlineData("revision")]
    public async Task Unbound_review_is_blocked_before_model(string fault)
    {
        var j = Fixture(); await j.Configure(); var request = j.ReviewRequest();
        request = fault switch {
            "manager" => request with { Counterpart = request.Counterpart with { OrganizationUserId = Guid.NewGuid() } },
            "project" => request with { WorkContext = request.WorkContext! with { WorkstreamId = Guid.NewGuid() } },
            "team" => request with { WorkContext = request.WorkContext! with { TeamId = Guid.NewGuid() } },
            _ => request
        };
        if (fault is "hash" or "revision")
            j.ReviewDocument = j.ReviewDocument! with { Revisions = [j.ReviewDocument.Revisions[0] with {
                Id = fault == "revision" ? Guid.NewGuid() : j.ReviewDocument.Revisions[0].Id,
                ContentSha256 = fault == "hash" ? "tampered" : j.ReviewDocument.Revisions[0].ContentSha256 }] };
        var result = await j.Agent.HandleCoordinationTurnAsync(request, j.Context, default);
        Assert.Equal("Blocked", result.Disposition); Assert.Equal(0, j.Models);
    }

    [Fact]
    public async Task Author_cannot_review_own_gdd()
    {
        var j = Fixture(); await j.Configure(); j.ModelText = null;
        await j.Agent.HandleCoordinationTurnAsync(j.Request(), j.Context, default);
        j.ReviewDocument = j.Documents.Values.Single();
        var result = await j.Agent.HandleCoordinationTurnAsync(j.ReviewRequest(), j.Context, default);
        Assert.Equal("Blocked", result.Disposition);
        Assert.Contains("own GDD", result.Content); Assert.Equal(1, j.Models);
    }

    private static CreativeScopeTests.Journey Fixture()
    {
        var j = new CreativeScopeTests.Journey(); var id = Guid.NewGuid(); var revision = Guid.NewGuid();
        j.ReviewDocument = new(id, "Production brief", "video-game.production-brief.v1", "Submitted",
            revision, revision, null, null, null,
            [new(revision, 1, null, "A single arena preserves the approved player promise.", new string('c', 64),
                "Submitted", DateTimeOffset.UtcNow, null, null)]) { WorkstreamId = j.Project, TeamId = j.Team };
        j.ModelText = JsonSerializer.Serialize(new CreativeReviewResult("accepted", "The arena and player promise match the pitch.", [], [], false));
        return j;
    }
}
