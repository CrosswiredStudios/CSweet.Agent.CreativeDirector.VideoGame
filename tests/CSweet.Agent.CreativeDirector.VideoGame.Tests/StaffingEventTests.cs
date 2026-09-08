using System.Text.Json;
using CSweet.Agent.SDK;
using CrosswiredStudios.VideoGame.Contracts;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class StaffingEventTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AttentionRecoversOnlyPendingRequestsAddressedToThisDirector(bool addressedToDirector)
    {
        var director = Guid.NewGuid(); var request = Guid.NewGuid(); Guid? readId = null;
        var pending = JsonSerializer.Deserialize<ResourceChangeRequestResponse>(JsonSerializer.Serialize(new
        { id = request, status = "Pending", managerOrganizationUserId = addressedToDirector ? director : Guid.NewGuid(),
          teamId = Guid.NewGuid(), workstreamId = Guid.NewGuid() }), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var runtime = new AgentTestRuntime().RegisterCapability<ResourceChangeReadRequest, ResourceChangeReadResponse>(
            "platform.management.resource-change.read.v1", (r, _) => {
                if (r.RequestId.HasValue) { readId = r.RequestId; return Task.FromResult(new ResourceChangeReadResponse([])); }
                return Task.FromResult(new ResourceChangeReadResponse([pending]));
            });
        var context = runtime.CreateContext(Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"),
            new AgentIdentity(director.ToString("D"), "Director", null, null, null, [], null, null, null));
        await VideoGameCreativeDirectorAgent.ReviewPendingStaffingAsync(context, default);
        Assert.Equal(addressedToDirector ? request : (Guid?)null, readId);
    }
    [Fact]
    public async Task CamelCaseHostEventReadsTheAuthoritativeRequest()
    {
        var director = Guid.NewGuid(); var request = Guid.NewGuid(); Guid? readId = null;
        var runtime = new AgentTestRuntime().RegisterCapability<ResourceChangeReadRequest, ResourceChangeReadResponse>(
            "platform.management.resource-change.read.v1", (r, _) =>
            { readId = r.RequestId; return Task.FromResult(new ResourceChangeReadResponse([])); });
        var context = runtime.CreateContext(Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"),
            new AgentIdentity(director.ToString("D"), "Director", null, null, null, [], null, null, null));
        var payload = JsonSerializer.SerializeToElement(new { requestId = request,
            managerOrganizationUserId = director, teamId = Guid.NewGuid(), workstreamId = Guid.NewGuid(), status = "Pending" });
        await new VideoGameCreativeDirectorAgent().HandleEventAsync(new AgentEventEnvelope(Guid.NewGuid(), Guid.NewGuid(),
            ManagementEvents.ResourceChangeRequested, payload, DateTimeOffset.UtcNow), context, default);
        Assert.Equal(request, readId);
    }

    [Fact]
    public void AssignedProducerCanBeReviewedWithoutChangingTheTeamLead()
    {
        var director = Guid.NewGuid(); var producer = Guid.NewGuid(); var installation = Guid.NewGuid();
        var member = new AgentTeammate(producer.ToString("D"), "Producer", "Agent", null, null, "DirectReport", "Available")
            { AgentInstallationId = installation, DeclaredRoleKeys = [VideoGameRoleKeys.Producer] };
        var roster = new AgentTeamContext(Guid.NewGuid().ToString("D"), "game", "Game", 1, director.ToString("D"), "Director", [member], [], 1, false);
        Assert.True(VideoGameCreativeDirectorAgent.CanReviewProducerCapacity(roster, director, producer, installation, director));
        Assert.False(VideoGameCreativeDirectorAgent.CanReviewProducerCapacity(roster, director, producer, Guid.NewGuid(), director));
        Assert.False(VideoGameCreativeDirectorAgent.CanReviewProducerCapacity(roster, director, producer, installation, Guid.NewGuid()));
        Assert.False(VideoGameCreativeDirectorAgent.CanReviewProducerCapacity(roster with { Members = [] }, director, producer, installation, director));
        Assert.False(VideoGameCreativeDirectorAgent.CanReviewProducerCapacity(roster with { Members = [member with { DeclaredRoleKeys = [] }] }, director, producer, installation, director));
    }
}
