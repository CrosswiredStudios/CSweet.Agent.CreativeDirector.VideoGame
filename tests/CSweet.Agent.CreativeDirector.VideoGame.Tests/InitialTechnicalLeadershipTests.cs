using System.Text.Json;
using CSweet.Agent.SDK;
using CrosswiredStudios.VideoGame.PitchCollaboration;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class InitialTechnicalLeadershipTests
{
    [Theory]
    [InlineData("game-technical-director", 1, true, "brief-sha", true)]
    [InlineData("game-engineer", 1, true, "brief-sha", false)]
    [InlineData("game-technical-director", 2, true, "brief-sha", false)]
    [InlineData("game-technical-director", 1, false, "brief-sha", false)]
    [InlineData("game-technical-director", 1, true, "stale", false)]
    public void InitialHireRequiresOneTechnicalLeadAndExactAcceptedBrief(string role, int count,
        bool approved, string evidence, bool expected)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var request = JsonSerializer.Deserialize<ResourceChangeRequestResponse>(JsonSerializer.Serialize(new {
            deltas = new[] { new { changeKind = "Add", role = new { roleKey = role, headcount = count } } },
            evidence = new[] { new { kind = "scope-capability-gap", sourceRevision = evidence, summary = "Technical decomposition" } }
        }), options)!;
        var roster = JsonSerializer.Deserialize<AgentTeamContext>("{\"members\":[]}", options)!;
        var review = new PitchReview("pitch", Guid.NewGuid(), Guid.NewGuid(), "brief-sha", true, [], "Scope is sufficiently defined.");
        var reply = new PitchReply("pitch", review.DocumentId, review.RevisionId, review.RevisionSha256, approved, "Agreed", "");
        var reason = VideoGameCreativeDirectorAgent.ValidateInitialTechnicalLeadership(request, roster, review, reply);
        Assert.Equal(expected, reason is null);
    }
}
