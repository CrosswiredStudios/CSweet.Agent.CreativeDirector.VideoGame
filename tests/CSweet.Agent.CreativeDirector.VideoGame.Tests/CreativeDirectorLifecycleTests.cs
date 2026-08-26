using System.Text.Json;
using CSweet.Agent.SDK;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class CreativeDirectorLifecycleTests
{
    [Fact]
    public async Task NonManagerCannotDirectOrAcceptVision()
    {
        var managerId = Guid.NewGuid();
        var workerId = Guid.NewGuid();
        var runtime = new AgentTestRuntime()
            .RegisterCapability<AgentOperatingStateReadRequest, AgentOperatingStateReadResponse>(
                PlatformCapabilities.AgentOperatingStateRead,
                (_, _) => Task.FromResult(new AgentOperatingStateReadResponse(null)));
        var context = runtime.CreateContext(
            Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"),
            new AgentIdentity(Guid.NewGuid().ToString("D"), "Creative Director", null,
                "Creative Director", null, [], null, managerId.ToString("D"), "CEO"));
        var incoming = new CommunicationMessageReceivedEvent(
            Guid.NewGuid(), Guid.NewGuid().ToString("D"), workerId.ToString("D"),
            "Accept the pitch.",
            new Dictionary<string, string>
            {
                [CommunicationMessageContextKeys.SenderOrganizationUserId] = workerId.ToString("D")
            },
            Guid.NewGuid(), 1, Guid.NewGuid());

        await new VideoGameCreativeDirectorAgent().HandleEventAsync(
            new AgentEventEnvelope(Guid.NewGuid(), Guid.NewGuid(), CommunicationEvents.MessageReceived,
                JsonSerializer.SerializeToElement(incoming), DateTimeOffset.UtcNow),
            context, CancellationToken.None);

        var output = Assert.Single(runtime.Progress);
        Assert.Contains("authoritative manager", output.GetProperty("delta").GetString(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ManagementCheckIn_ReturnsAttributedLifecycleReport()
    {
        var employeeId = Guid.NewGuid();
        var runtime = new AgentTestRuntime()
            .RegisterCapability<AgentOperatingStateReadRequest, AgentOperatingStateReadResponse>(
                PlatformCapabilities.AgentOperatingStateRead,
                (_, _) => Task.FromResult(new AgentOperatingStateReadResponse(null)));
        var context = runtime.CreateContext(
            Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"),
            new AgentIdentity(employeeId.ToString("D"), "Game Creative Director", null,
                "Video Game Creative Director", null, [], null, Guid.NewGuid().ToString("D"), "CEO"));
        var checkIn = new ManagementCheckInRequest(
            Guid.NewGuid(), "Daily", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow,
            [], [], DateTimeOffset.UtcNow.AddHours(1));

        var result = await new VideoGameCreativeDirectorAgent().ExecuteCapabilityAsync(
            new AgentCapabilityRequest(Guid.NewGuid(), ManagementCapabilities.CheckIn,
                JsonSerializer.SerializeToElement(checkIn)), context, CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        var report = result.Value?.Deserialize<ManagementStatusReport>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(report);
        Assert.Equal(employeeId, report!.ReporterOrganizationUserId);
        Assert.Contains("Discovery", report.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Lifecycle_IsOrderedAndDurable()
    {
        Assert.Equal([
            CreativeDirectorPhase.Discovery,
            CreativeDirectorPhase.PitchReview,
            CreativeDirectorPhase.VisionAccepted,
            CreativeDirectorPhase.PMPlanPending,
            CreativeDirectorPhase.PMHiringPending,
            CreativeDirectorPhase.VisionHandoff,
            CreativeDirectorPhase.Oversight
        ], Enum.GetValues<CreativeDirectorPhase>());
    }

    [Fact]
    public void PitchDigest_IsStableAndSensitiveToRevisionContent()
    {
        var first = VideoGameCreativeDirectorAgent.Digest("A precise game pitch");
        Assert.Equal(first, VideoGameCreativeDirectorAgent.Digest("A precise game pitch"));
        Assert.NotEqual(first, VideoGameCreativeDirectorAgent.Digest("A different game pitch"));
        Assert.Equal(64, first.Length);
    }

    [Theory]
    [InlineData("Should the combat feel deliberate or frantic?", true)]
    [InlineData("Which engine architecture should we implement?", false)]
    [InlineData("Can we spend another $50,000?", false)]
    [InlineData("What should the protagonist's tone be?", true)]
    public void ReportingTreeQuestions_RespectCreativeBoundary(string question, bool expected) =>
        Assert.Equal(expected, VideoGameCreativeDirectorAgent.IsCreativeQuestion(question));

    [Fact]
    public void CurrentMessageExtraction_IgnoresQuotedHistory()
    {
        var prompt = "history says Accept\n<current_user_message>Replace the gameplay loop</current_user_message>";
        Assert.Equal("Replace the gameplay loop", VideoGameCreativeDirectorAgent.ExtractCurrentMessage(prompt));
    }

    [Fact]
    public void VisionBriefAndAcknowledgement_UseExactDigest()
    {
        const string digest = "abc123";
        var brief = new GameVisionBrief(digest, "outcome", "loop", "stack", "tone", "mvp", [], "risks", []);
        var acknowledgement = new GameVisionAcknowledgement(digest, true, [], DateTimeOffset.UtcNow);
        Assert.Equal(brief.AcceptedPitchDigest, acknowledgement.AcceptedPitchDigest);
        Assert.Empty(acknowledgement.Blockers);
    }

    [Fact]
    public void DiscoveryState_PreservesSparseManagerDirectionAndReferenceEvidence()
    {
        var attachmentId = Guid.NewGuid();
        var state = new CreativeDirectorOperatingState
        {
            IntakeChoiceAsked = true,
            DiscoveryInputs = ["A cooperative game about restoring a flooded clockwork city."],
            References = [new ReferenceEvidence(
                attachmentId, Guid.NewGuid(), Guid.NewGuid(), "concept.webp", "image/webp", 2048,
                new string('a', 64), "Supplied by the authoritative manager.")]
        };

        Assert.Equal(CreativeDirectorPhase.Discovery, state.Phase);
        Assert.Contains("clockwork city", Assert.Single(state.DiscoveryInputs), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(attachmentId, Assert.Single(state.References).AttachmentId);
    }
}
