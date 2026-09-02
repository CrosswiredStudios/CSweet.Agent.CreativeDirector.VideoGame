using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.Memory;
using CSweet.VideoGame.Contracts;

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
    public void Lifecycle_ExecutesOnlyGovernedTransitions()
    {
        var delegatedPath = new[]
        {
            CreativeDirectorPhase.Discovery,
            CreativeDirectorPhase.InvolvementConfirmation,
            CreativeDirectorPhase.HighLevelAccepted,
            CreativeDirectorPhase.TeamPlanPending,
            CreativeDirectorPhase.TeamStaffingPending,
            CreativeDirectorPhase.WorkstreamPlanPending,
            CreativeDirectorPhase.ProjectSetup,
            CreativeDirectorPhase.DetailedDesign,
            CreativeDirectorPhase.PackageReview,
            CreativeDirectorPhase.Oversight
        };

        Assert.All(delegatedPath.Zip(delegatedPath.Skip(1)), transition =>
            Assert.True(VideoGameCreativeDirectorAgent.IsAllowedPhaseTransition(
                transition.First, transition.Second), $"{transition.First} -> {transition.Second}"));
        Assert.False(VideoGameCreativeDirectorAgent.IsAllowedPhaseTransition(
            CreativeDirectorPhase.Discovery, CreativeDirectorPhase.Oversight));
        Assert.False(VideoGameCreativeDirectorAgent.IsAllowedPhaseTransition(
            CreativeDirectorPhase.Oversight, CreativeDirectorPhase.DetailedDesign));
    }

    [Fact]
    public void PitchDigest_IsStableAndSensitiveToRevisionContent()
    {
        var first = VideoGameCreativeDirectorAgent.Digest("A precise game pitch");
        Assert.Equal(first, VideoGameCreativeDirectorAgent.Digest("A precise game pitch"));
        Assert.NotEqual(first, VideoGameCreativeDirectorAgent.Digest("A different game pitch"));
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void ProjectStateKeysIsolateConcurrentWorkstreamsAndIntakeConversations()
    {
        var firstWorkstream = Guid.NewGuid();
        var secondWorkstream = Guid.NewGuid();
        var intake = Guid.NewGuid();

        var keys = new[]
        {
            VideoGameCreativeDirectorAgent.ProjectStateKey(firstWorkstream, null),
            VideoGameCreativeDirectorAgent.ProjectStateKey(secondWorkstream, null),
            VideoGameCreativeDirectorAgent.ProjectStateKey(null, intake),
            VideoGameCreativeDirectorAgent.PortfolioStateKey
        };

        Assert.Equal(keys.Length, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(firstWorkstream.ToString("N"), keys[0]);
        Assert.Contains(intake.ToString("N"), keys[2]);
    }

    [Fact]
    public void DeterministicArtifactReviewRejectsPlaceholderAndUnstructuredContent()
    {
        var findings = VideoGameCreativeDirectorAgent.DeterministicArtifactFindings(
            "TODO: replace this placeholder with a real design.");

        Assert.Contains(findings, finding => finding.Code == "insufficient-substance" && finding.Blocking);
        Assert.Contains(findings, finding => finding.Code == "placeholder-content" && finding.Blocking);
        Assert.Contains(findings, finding => finding.Code == "missing-structure" && finding.Blocking);
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
    [InlineData("Decision: How involved do you want to be in creative direction?\nAnswer: Review milestones", true)]
    [InlineData("I just want to set the initial direction and then review milestones.", true)]
    [InlineData("Review milestones for a cozy horror game about restoring a flooded city.", false)]
    [InlineData("Collaborate closely on a PC strategy game.", false)]
    public void InteractionOnlyRepliesDoNotStartPitchProduction(string message, bool expected) =>
        Assert.Equal(expected, VideoGameCreativeDirectorAgent.IsInteractionPreferenceOnly(message, []));

    [Fact]
    public void PitchGenerationFailuresAreRecoverableUnlessTheWorkItemWasCancelled()
    {
        Assert.True(VideoGameCreativeDirectorAgent.IsRecoverablePitchGenerationFailure(
            new HttpRequestException("transport failed"), CancellationToken.None));
        Assert.True(VideoGameCreativeDirectorAgent.IsRecoverablePitchGenerationFailure(
            new TimeoutException("model timed out"), CancellationToken.None));
        Assert.True(VideoGameCreativeDirectorAgent.IsRecoverablePitchGenerationFailure(
            new OperationCanceledException("request timeout"), CancellationToken.None));
        Assert.True(VideoGameCreativeDirectorAgent.IsRecoverablePitchGenerationFailure(
            new InvalidOperationException("The configured model returned an empty game pitch."),
            CancellationToken.None));
        Assert.True(VideoGameCreativeDirectorAgent.IsRecoverablePitchGenerationFailure(
            new PlatformCapabilityException(
                PlatformCapabilities.LlmChatStream,
                PlatformCapabilityErrorCode.Unavailable,
                "model unavailable"),
            CancellationToken.None));

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.False(VideoGameCreativeDirectorAgent.IsRecoverablePitchGenerationFailure(
            new OperationCanceledException(cancelled.Token), cancelled.Token));
    }

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
    public void StudioPlanContainsFourteenDistinctAccountableRolesLedByProducer()
    {
        var creativeDirectorId = Guid.NewGuid();
        var roles = VideoGameCreativeDirectorAgent.BuildRequiredStudioRoles(creativeDirectorId);

        Assert.Equal(14, roles.Count);
        Assert.Equal(14, roles.Select(x => x.RoleKey).Distinct(StringComparer.Ordinal).Count());
        Assert.All(roles, role => Assert.Equal(1, role.Headcount));
        var producer = Assert.Single(roles, role => role.RoleKey == VideoGameRoleKeys.Producer);
        Assert.Equal(creativeDirectorId, producer.ReportsToOrganizationUserId);
        Assert.All(roles.Where(role => role.RoleKey != VideoGameRoleKeys.Producer), role =>
            Assert.Equal(VideoGameRoleKeys.Producer, role.ReportsToRoleKey));
    }

    [Theory]
    [InlineData("web browser Phaser 2D", VideoGameToolchainRecipeKeys.PhaserWeb2D)]
    [InlineData("web browser Babylon 3D", VideoGameToolchainRecipeKeys.BabylonWeb3D)]
    [InlineData("Windows native Godot 2D", VideoGameToolchainRecipeKeys.GodotNative2DGdscript)]
    [InlineData("Linux native Godot 3D", VideoGameToolchainRecipeKeys.GodotNative3DGdscript)]
    public void ToolchainRecipesFollowLockedTargetAndDimensionalityRules(string direction, string expected)
    {
        var state = new CreativeDirectorOperatingState
        {
            ManagerPreferences = new ManagerPreferenceProfile { PlatformConstraints = [direction] }
        };

        Assert.Equal(expected, VideoGameCreativeDirectorAgent.DetermineRequiredRecipe(state));
    }

    [Theory]
    [InlineData("Asset strategy: provided; use only uploaded project assets.", VideoGameAssetProductionModes.Provided)]
    [InlineData("Use procedural assets and code-native geometry.", VideoGameAssetProductionModes.Procedural)]
    [InlineData("Asset strategy: generative with configured providers.", VideoGameAssetProductionModes.Generative)]
    [InlineData("Use procedural and uploaded assets as a hybrid.", VideoGameAssetProductionModes.Hybrid)]
    public void AssetStrategyPreferenceIsExplicitAndProjectScoped(string direction, string expected)
    {
        var preferences = VideoGameCreativeDirectorAgent.UpdateManagerPreferences(
            new ManagerPreferenceProfile(), direction, Guid.NewGuid(), [], applyDefault: true);

        Assert.Equal(expected, preferences.AssetStrategyPreference);
    }

    [Fact]
    public void ThreeProjectAcceptanceProgramIsIsolatedAndGovernedThroughStabilization()
    {
        var scenarios = new[]
        {
            Scenario("Phaser microgame", "web browser Phaser 2D", VideoGameAssetProductionModes.Procedural,
                VideoGameToolchainRecipeKeys.PhaserWeb2D, ["web"], 25_000m),
            Scenario("Babylon microgame", "web browser Babylon 3D", VideoGameAssetProductionModes.Hybrid,
                VideoGameToolchainRecipeKeys.BabylonWeb3D, ["web"], 35_000m),
            Scenario("Godot puzzle", "Windows Linux native Godot 3D", VideoGameAssetProductionModes.Provided,
                VideoGameToolchainRecipeKeys.GodotNative3DGdscript, ["windows-x64", "linux-x64"], 50_000m)
        };

        Assert.Equal(3, scenarios.Select(x => x.WorkstreamId).Distinct().Count());
        Assert.Equal(3, scenarios.Select(x => x.TeamId).Distinct().Count());
        Assert.Equal(3, scenarios.Select(x => x.BoardId).Distinct().Count());
        Assert.Equal(3, scenarios.Select(x => x.RepositoryId).Distinct().Count());
        Assert.Equal(3, scenarios.Select(x => x.StateKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(3, scenarios.Select(x => x.Budget).Distinct().Count());
        Assert.All(scenarios, scenario =>
        {
            Assert.Equal(scenario.ExpectedRecipe,
                VideoGameCreativeDirectorAgent.DetermineRequiredRecipe(scenario.State));
            Assert.Equal(14, VideoGameCreativeDirectorAgent.BuildRequiredStudioRoles(Guid.NewGuid())
                .Select(x => x.RoleKey).Distinct(StringComparer.Ordinal).Count());
        });

        var milestones = VideoGameCreativeDirectorAgent.BuildLifecycleMilestones(DateTimeOffset.UtcNow);
        var requiredStages = new[]
        {
            VideoGameLifecyclePhases.Concept, VideoGameLifecyclePhases.PreProduction,
            VideoGameLifecyclePhases.Prototype, VideoGameLifecyclePhases.VerticalSlice,
            VideoGameLifecyclePhases.Production, VideoGameLifecyclePhases.Alpha,
            VideoGameLifecyclePhases.Beta, VideoGameLifecyclePhases.ReleaseCandidate,
            VideoGameLifecyclePhases.Launch, VideoGameLifecyclePhases.PostLaunchStabilization
        };
        Assert.All(requiredStages, stage => Assert.Contains(milestones, x => x.LifecycleStage == stage));
        var launch = Assert.Single(milestones, x => x.Key == VideoGameMilestoneKeys.LaunchApproved);
        Assert.Equal(["human-owner"], launch.RequiredReviewerRoleKeys);
        Assert.Contains("video-game.release-readiness.v1", launch.RequiredEvidenceTypeKeys);
    }

    private static AcceptanceScenario Scenario(string title, string direction, string strategy,
        string recipe, IReadOnlyList<string> targets, decimal budget)
    {
        var workstreamId = Guid.NewGuid();
        return new AcceptanceScenario(workstreamId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            VideoGameCreativeDirectorAgent.ProjectStateKey(workstreamId, null), budget, strategy, recipe, targets,
            new CreativeDirectorOperatingState
            {
                WorkstreamId = workstreamId,
                WorkingTitle = title,
                ManagerPreferences = new ManagerPreferenceProfile
                {
                    PlatformConstraints = [direction],
                    AssetStrategyPreference = strategy
                }
            });
    }

    private sealed record AcceptanceScenario(Guid WorkstreamId, Guid TeamId, Guid BoardId, Guid RepositoryId,
        string StateKey, decimal Budget, string AssetStrategy, string ExpectedRecipe,
        IReadOnlyList<string> Targets, CreativeDirectorOperatingState State);
}
