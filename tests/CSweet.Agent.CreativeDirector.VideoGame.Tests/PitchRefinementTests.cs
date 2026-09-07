using System.Text.Json;
using CSweet.Agent.SDK;
using CrosswiredStudios.VideoGame.PitchCollaboration;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class PitchRefinementTests
{
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task BothProducerConfidenceAndCreativeAcceptanceAreRequired(bool producerReady, bool directorAccepts, bool expectedAcceptance)
    {
        var producer = Guid.NewGuid(); var director = Guid.NewGuid(); var workstream = Guid.NewGuid(); var team = Guid.NewGuid();
        var session = Guid.NewGuid(); var document = Guid.NewGuid(); var revision = Guid.NewGuid(); var decisions = 0;
        var markdown = string.Join("\n", new[] { "Scope", "Player experience", "Non-goals", "Acceptance criteria", "Deliverables", "Constraints", "Risks", "Open questions" }.Select(x => $"## {x}\nDetail."));
        var state = new CreativeDirectorOperatingState { WorkstreamId = workstream, TeamId = team, ProducerEmployeeId = producer,
            AcceptedVision = new(1, "pitch", "Accepted arcade pitch", Guid.NewGuid(), Guid.NewGuid(), "pitch-sha", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow) };
        var runtime = new AgentTestRuntime()
            .RegisterCapability<JsonElement, ArtifactDocument>(PlatformCapabilities.ArtifactRead, (_, _) => Task.FromResult(
                new ArtifactDocument(document, "Planning", PitchProtocol.DocumentType, "InReview", revision, revision, null, null, null,
                    [new(revision, 1, null, markdown, "draft-hash", "Submitted", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null)]) { WorkstreamId = workstream, TeamId = team }))
            .RegisterCapability<AgentOperatingStateReadRequest, AgentOperatingStateReadResponse>(PlatformCapabilities.AgentOperatingStateRead,
                (request, _) => Task.FromResult(new AgentOperatingStateReadResponse(new(Guid.NewGuid(), request.StateKey, "test", 1, "Active",
                    new Dictionary<string, string>(), [], "cached", [], Guid.NewGuid(), JsonSerializer.SerializeToElement(new DirectorReview(directorAccepts, "Answers and scope assessment", markdown)), 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow))))
            .RegisterCapability<DecideArtifactRevision, ArtifactDocument>(PlatformCapabilities.ArtifactDecide,
                (request, _) => { decisions++; Assert.Equal(revision, request.RevisionId); return Task.FromResult(new ArtifactDocument(document, "Planning", PitchProtocol.DocumentType, "Approved", revision, null, revision, null, null, [])); });
        var request = new AgentCoordinationTurnRequest(session, 2, 2, "Pitch", "Refine", [],
            new(director, Guid.NewGuid(), "Director", "Director"), new(producer, Guid.NewGuid(), "Producer", "Producer"), false, [])
            { WorkContext = new(Guid.NewGuid(), workstream, team, Guid.NewGuid(), null, null, null, Guid.NewGuid(), null, null) };
        var result = await new VideoGameCreativeDirectorAgent().ReviewProducerPitchAsync(request, state,
            new("pitch", document, revision, "draft-hash", producerReady, producerReady ? [] : ["What is the release platform?"], "Planning assessment"), runtime.CreateContext(), default);
        Assert.Equal(AgentCoordinationDispositions.Continue, result.Disposition);
        var reply = result.Artifact!.Payload.Deserialize<PitchReply>(PitchProtocol.Json)!;
        Assert.Equal(expectedAcceptance, reply.Accepted); Assert.Equal(expectedAcceptance ? 1 : 0, decisions);
    }
}
