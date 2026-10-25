using System.Text.Json;
using CSweet.Agent.SDK;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class CreativeScopeTests
{
    [Fact]
    public async Task Manifest_and_runtime_limit_creative_authority()
    {
        var manifest = await AgentManifestLoader.LoadAsync(Path.Combine(AppContext.BaseDirectory, "csweet-plugin.json"), default);
        var agent = new VideoGameCreativeDirectorAgent();
        Assert.Equal(agent.AgentId, manifest.Id);
        Assert.Equal(agent.Version, manifest.Version);
        Assert.Equal(agent.Version, typeof(VideoGameCreativeDirectorAgent).Assembly.GetName().Version!.ToString(3));
        Assert.Equal("individual-contributor.v1", manifest.RolePolicy!.Profile);
        Assert.Equal(["creative-director"], manifest.RolePolicy.DeclaredRoleKeys);
        Assert.Empty(manifest.WorkstreamProfiles.Provides);
        Assert.DoesNotContain(manifest.Requires, x => x.Name.Contains("resource-change") || x.Name.Contains("project-approval") || x.Name.Contains("artifact.decide"));
        Assert.Contains(manifest.Capabilities, x => x == "work.execution.run");
        Assert.Contains(manifest.Capabilities, x => x == ManagementCapabilities.CheckIn);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exact_approved_pitch_is_required_before_creative_model_work(bool wrongManager)
    {
        var journey = new Journey();
        var request = journey.Request();
        if (wrongManager) request = request with { Counterpart = request.Counterpart with { OrganizationUserId = Guid.NewGuid() } };
        else journey.Accepted = false;
        if (wrongManager)
            Assert.Equal("Blocked", (await journey.Agent.HandleCoordinationTurnAsync(request, journey.Context, default)).Disposition);
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => journey.Agent.HandleCoordinationTurnAsync(request, journey.Context, default));
        Assert.Equal(0, journey.Models);
        Assert.Empty(journey.Documents);
    }

    [Fact]
    public async Task Gdd_submission_replays_without_regenerating_or_duplicating_document()
    {
        var journey = new Journey();
        await journey.Configure();
        var first = await journey.Agent.HandleCoordinationTurnAsync(journey.Request(), journey.Context, default);
        var retry = await journey.Agent.HandleCoordinationTurnAsync(journey.Request(), journey.Context, default);
        Assert.Equal("Continue", first.Disposition);
        Assert.Equal(first.Artifact!.Payload.GetRawText(), retry.Artifact!.Payload.GetRawText());
        Assert.Equal(1, journey.Models);
        Assert.Single(journey.Documents);
        Assert.Equal(journey.Manager, journey.Reviewer);
        Assert.Equal(journey.Project, journey.Documents.Values.Single().WorkstreamId);
    }

    [Fact]
    public void Incomplete_creative_output_is_not_a_submittable_gdd()
    {
        Assert.False(VideoGameCreativeDirectorAgent.ValidDesign("## Gameplay\nSome ideas"));
        Assert.True(VideoGameCreativeDirectorAgent.ValidDesign(Journey.Markdown));
    }

    private sealed class Journey
    {
        public readonly Guid Manager = Guid.NewGuid(), Creative = Guid.NewGuid(), Project = Guid.NewGuid(), Team = Guid.NewGuid();
        private readonly Guid _pitch = Guid.NewGuid(), _revision = Guid.NewGuid();
        private readonly string _hash = new('a', 64);
        private readonly Dictionary<string, AgentOperatingStateResponse> _states = [];
        public readonly Dictionary<string, ArtifactDocument> Documents = [];
        public readonly VideoGameCreativeDirectorAgent Agent = new();
        public AgentRuntimeContext Context { get; }
        public bool Accepted = true;
        public int Models;
        public Guid? Reviewer;
        public static string Markdown => string.Join("\n", VideoGameCreativeDirectorAgent.DesignSections.Select(x => $"## {x}\nConcrete direction."));
        public Journey()
        {
            var runtime = new AgentTestRuntime()
                .RegisterCapability<JsonElement, ArtifactDocument>(PlatformCapabilities.ArtifactRead, (_, _) => Task.FromResult(
                    new ArtifactDocument(_pitch, "Official pitch", "video-game.vision.v1", "Approved", _revision, null, _revision, null, null,
                        [new(_revision, 1, null, "Bounded game", _hash, Accepted ? "Accepted" : "Draft", DateTimeOffset.UtcNow, null, null)])))
                .RegisterCapability<AgentOperatingStateReadRequest, AgentOperatingStateReadResponse>(PlatformCapabilities.AgentOperatingStateRead,
                    (r, _) => Task.FromResult(new AgentOperatingStateReadResponse(_states.GetValueOrDefault(r.StateKey))))
                .RegisterCapability<AgentOperatingStateWriteRequest, AgentOperatingStateResponse>(PlatformCapabilities.AgentOperatingStateWrite,
                    (r, _) => Task.FromResult(_states[r.StateKey] = new(Guid.NewGuid(), r.StateKey, r.SchemaId, r.SchemaVersion,
                        r.Status, r.SourceRevisions, r.ConditionCodes, r.DecisionFingerprint, r.OpenCommitmentCorrelations,
                        r.AttentionReviewId, r.Payload, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)))
                .RegisterCapability<JsonElement, JsonElement>(PlatformCapabilities.LlmChatStream, (_, _) => {
                    Models++; return Task.FromResult(JsonSerializer.SerializeToElement(new { text = Markdown, role = "assistant" })); })
                .RegisterCapability<CreateArtifactDocument, ArtifactDocument>(PlatformCapabilities.ArtifactCreate, (r, _) => {
                    if (Documents.TryGetValue(r.IdempotencyKey, out var existing)) return Task.FromResult(existing);
                    var revision = Guid.NewGuid();
                    var doc = new ArtifactDocument(Guid.NewGuid(), r.Title, r.DocumentType, "Draft", revision, null, null, null, null,
                        [new(revision, 1, null, r.Content, new string('b', 64), "Draft", DateTimeOffset.UtcNow, null, null)])
                        { WorkstreamId = r.WorkstreamId, TeamId = r.TeamId };
                    Documents.Add(r.IdempotencyKey, doc); return Task.FromResult(doc); })
                .RegisterCapability<SubmitArtifactRevision, ArtifactDocument>(PlatformCapabilities.ArtifactSubmit, (r, _) => {
                    Reviewer = r.ReviewerOrganizationUserId; return Task.FromResult(Documents.Values.Single(x => x.Id == r.ArtifactId)); });
            Context = runtime.CreateContext(identity: new(Creative.ToString(), "Creative", null, "Creative Director", null, [], null, Manager.ToString(), "Video Game Director"));
        }
        public async Task Configure() => Assert.True((await Agent.ExecuteCapabilityAsync(new(Guid.NewGuid(), AgentConfigurationCapabilities.Update,
            JsonSerializer.SerializeToElement(new UpdateAgentConfigurationRequest(new Dictionary<string,JsonElement> {
                ["llmProviderId"] = JsonSerializer.SerializeToElement(Guid.NewGuid()), ["llmModel"] = JsonSerializer.SerializeToElement("test") }))), Context, default)).Succeeded);
        public AgentCoordinationTurnRequest Request() => new(Guid.NewGuid(), 1, 3, "GDD", "Develop approved game", [],
            new(Creative, Guid.NewGuid(), "Creative", "Target"), new(Manager, Guid.NewGuid(), "Director", "Initiator"), false,
            [new(Guid.NewGuid(), 0, Manager, "Continue", "Develop GDD", DateTimeOffset.UtcNow,
                new(VideoGameCreativeDirectorAgent.DesignRequestType, "1.0", _hash, 1, true,
                    JsonSerializer.SerializeToElement(new CreativeDesignRequest(Project, Team, Manager, Creative, Guid.NewGuid(), _pitch, _revision, _hash), new JsonSerializerOptions(JsonSerializerDefaults.Web)), "request-digest"))])
            { WorkContext = new(Guid.NewGuid(), Project, Team, null, null, null, null, Guid.NewGuid(), null, null) };
    }
}
