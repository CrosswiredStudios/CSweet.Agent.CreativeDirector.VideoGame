using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.Memory;

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

    [Theory]
    [InlineData("Delegate unspecified decisions to you.", ManagerInvolvementMode.Delegated)]
    [InlineData("I want to review major milestones.", ManagerInvolvementMode.MilestoneReview)]
    [InlineData("Let's collaborate closely.", ManagerInvolvementMode.Collaborative)]
    public void InvolvementCalibrationRecognizesAllLifecycleModes(
        string message,
        ManagerInvolvementMode expected) =>
        Assert.Equal(expected, VideoGameCreativeDirectorAgent.ParseInvolvementMode(message));

    [Theory]
    [InlineData(ManagerInvolvementMode.Delegated, "LockAndStaff")]
    [InlineData(ManagerInvolvementMode.MilestoneReview, "AwaitExplicitMilestoneApproval")]
    [InlineData(ManagerInvolvementMode.Collaborative, "IterateCollaboratively")]
    public void InvolvementModeSelectsIntendedVisionApprovalPath(
        ManagerInvolvementMode mode,
        string expected) =>
        Assert.Equal(expected, VideoGameCreativeDirectorAgent.InitialVisionDisposition(mode));

    [Fact]
    public void ManagerPreferencesPersistExplicitConstraintsAndEvidence()
    {
        var messageId = Guid.NewGuid();
        var preferences = VideoGameCreativeDirectorAgent.UpdateManagerPreferences(
            new ManagerPreferenceProfile(),
            "Delegate decisions. Target PC and Steam for a cooperative strategy game. I do not want to be involved in story decisions.",
            messageId,
            [],
            applyDefault: true);

        Assert.Equal(ManagerInvolvementMode.Delegated, preferences.InvolvementMode);
        Assert.Contains("PC", preferences.PlatformConstraints);
        Assert.Contains("Steam", preferences.PlatformConstraints);
        Assert.Contains("strategy", preferences.GenreConstraints, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Delegate story", preferences.StoryParticipation, StringComparison.Ordinal);
        Assert.Contains(messageId, preferences.SupportingMessageIds);
        Assert.NotNull(preferences.UpdatedAt);
    }

    [Fact]
    public void PlatformParsingDoesNotMistakeConceptForPc()
    {
        var preferences = VideoGameCreativeDirectorAgent.UpdateManagerPreferences(
            new ManagerPreferenceProfile(),
            "Use the current concept and make autonomous creative recommendations.",
            Guid.NewGuid(), [], applyDefault: true);

        Assert.DoesNotContain("PC", preferences.PlatformConstraints);
    }

    [Fact]
    public void ExplicitNarrativeDirectionIsProjectStateNotUserParticipation()
    {
        var preferences = VideoGameCreativeDirectorAgent.UpdateManagerPreferences(
            new ManagerPreferenceProfile(),
            "The story follows siblings restoring a flooded clockwork city.",
            Guid.NewGuid(), [], applyDefault: true);

        Assert.Null(preferences.StoryParticipation);
        Assert.Contains(preferences.NarrativeConstraints,
            x => x.Contains("clockwork city", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExplicitMemoryProposalsUseUserAndBusinessScopesWithoutRawAttachmentNames()
    {
        var messageId = Guid.NewGuid();
        var incoming = new CommunicationMessageReceivedEvent(
            Guid.NewGuid(), Guid.NewGuid().ToString("D"), "manager-user",
            "Collaborate closely on a PC RPG; I want to review the story.",
            new Dictionary<string, string>(), Guid.NewGuid(), 1, messageId)
        {
            Attachments = [new CommunicationAttachment(
                Guid.NewGuid(), messageId, "C:\\private\\concept.png", "image/png", 4096, new string('a', 64))]
        };

        var proposals = VideoGameCreativeDirectorAgent.BuildExplicitMemoryProposals(
            incoming, Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"));

        Assert.Equal(2, proposals.Count);
        Assert.Contains(proposals, x => x.Scope == MemoryScope.User && x.Sensitivity == MemorySensitivity.Personal);
        Assert.Contains(proposals, x => x.Scope == MemoryScope.Tenant && x.Sensitivity == MemorySensitivity.Internal);
        Assert.All(proposals, x => Assert.DoesNotContain("C:\\private", x.Content, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(proposals, x => x.Content.Contains(new string('a', 64), StringComparison.Ordinal));
    }

    [Fact]
    public void ProductManagerPlanContainsOneDirectReportToCreativeDirector()
    {
        var creativeDirectorId = Guid.NewGuid();
        var role = VideoGameCreativeDirectorAgent.BuildProductManagerRole(creativeDirectorId);

        Assert.Equal("product-manager", role.RoleKey);
        Assert.Equal(1, role.Headcount);
        Assert.Equal(creativeDirectorId, role.ReportsToOrganizationUserId);
        Assert.Equal(["product-management.plan.v1"], role.RequiredCapabilities);
    }
}
