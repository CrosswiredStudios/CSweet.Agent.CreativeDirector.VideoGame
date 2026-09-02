using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;
using Microsoft.Extensions.AI;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class CreativeDirectorInteractionTests
{
    [Fact]
    public async Task ModelStreamingPublishesReasoningAndDraftToTheConversationTurn()
    {
        var runtime = new AgentTestRuntime();
        var turnId = Guid.NewGuid();
        string response;

        await using (var stream = runtime.CreateContext().CreateTurnStream(
                         Guid.NewGuid().ToString("D"), turnId, 1))
        {
            response = await VideoGameCreativeDirectorAgent.StreamAssistantResponseAsync(
                new ReasoningChatClient(),
                [new ChatMessage(ChatRole.User, "Create a concise game direction.")],
                new ChatOptions(),
                stream,
                CancellationToken.None);
        }

        Assert.Equal("A focused creative answer.", response);
        var reasoning = Assert.Single(runtime.Progress,
            progress => progress.GetProperty("kind").GetString() == AgentTurnStreamKinds.ReasoningDelta);
        Assert.Equal("I should preserve the requested tone and scope.",
            reasoning.GetProperty("delta").GetString());
        Assert.Contains(runtime.Progress,
            progress => progress.GetProperty("kind").GetString() == AgentTurnStreamKinds.ReasoningCompleted);
        var draft = Assert.Single(runtime.Progress,
            progress => progress.GetProperty("kind").GetString() == AgentTurnStreamKinds.DraftDelta);
        Assert.Equal(response, draft.GetProperty("delta").GetString());
    }

    [Theory]
    [InlineData("Thanks!", (int)CreativeDirectorInboundDisposition.Acknowledge)]
    [InlineData("What is the project status?", (int)CreativeDirectorInboundDisposition.StatusRequest)]
    [InlineData("How does the core loop support the player promise?", (int)CreativeDirectorInboundDisposition.InformationQuestion)]
    [InlineData("Please explore three sequel directions.", (int)CreativeDirectorInboundDisposition.DurableAction)]
    [InlineData("Decision: How involved?\nAnswer: Review milestones", (int)CreativeDirectorInboundDisposition.WorkflowInput)]
    [InlineData("Make it a cooperative puzzle game.", (int)CreativeDirectorInboundDisposition.WorkflowInput)]
    public void InboundPolicySeparatesConversationFromDurableWork(
        string message,
        int expected) =>
        Assert.Equal((CreativeDirectorInboundDisposition)expected, CreativeDirectorInteractionPolicy.Classify(message));

    [Fact]
    public void AgendaCorrelationsAndTitlesAreStableAndBounded()
    {
        var conversationId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        Assert.Equal(CreativeDirectorAgenda.VisionCorrelation(conversationId),
            CreativeDirectorAgenda.VisionCorrelation(conversationId));
        Assert.Equal(CreativeDirectorAgenda.ChatActionCorrelation(messageId),
            CreativeDirectorAgenda.ChatActionCorrelation(messageId));
        Assert.Equal(CreativeDirectorAgenda.ProjectReviewCorrelation(conversationId),
            CreativeDirectorAgenda.ProjectReviewCorrelation(conversationId));
        Assert.True(CreativeDirectorAgenda.TaskTitle(new string('a', 500)).Length <= 114);
        Assert.Equal(TimeSpan.FromDays(1),
            CreativeDirectorAgenda.ProjectReviewCadence(CreativeDirectorPhase.Oversight));
        Assert.Equal(TimeSpan.FromHours(4),
            CreativeDirectorAgenda.ProjectReviewCadence(CreativeDirectorPhase.DetailedDesign));
    }

    [Fact]
    public async Task AcceptedVisionCompletesItsDurablePersonalCard()
    {
        var conversationId = Guid.NewGuid();
        var workstreamId = Guid.NewGuid();
        var artifactId = Guid.NewGuid();
        var artifactRevisionId = Guid.NewGuid();
        const string hash = "abc123";
        var stateKey = VideoGameCreativeDirectorAgent.ProjectStateKey(workstreamId, null);
        var state = new CreativeDirectorOperatingState
        {
            IntakeConversationId = conversationId,
            WorkstreamId = workstreamId,
            Phase = CreativeDirectorPhase.HighLevelAccepted,
            AcceptedVision = new AcceptedGameVision(
                1, "pitch-digest", "# Accepted game", artifactId, artifactRevisionId, hash,
                conversationId, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow)
        };
        var portfolio = new CreativeDirectorPortfolioIndex
        {
            Projects = [new CreativeDirectorPortfolioEntry(
                stateKey, workstreamId, conversationId, null, null, "Accepted game",
                CreativeDirectorPhase.HighLevelAccepted, DateTimeOffset.UtcNow)]
        };
        var runtime = new AgentTestRuntime()
            .RegisterCapability<AgentOperatingStateReadRequest, AgentOperatingStateReadResponse>(
                PlatformCapabilities.AgentOperatingStateRead,
                (request, _) => Task.FromResult(new AgentOperatingStateReadResponse(
                    request.StateKey == VideoGameCreativeDirectorAgent.PortfolioStateKey
                        ? OperatingState(request.StateKey, portfolio)
                        : request.StateKey == stateKey
                            ? OperatingState(request.StateKey, state)
                            : null)));
        var context = runtime.CreateContext(
            Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D"),
            new AgentIdentity(Guid.NewGuid().ToString("D"), "Creative Director", null,
                "Creative Director", null, [], null, Guid.NewGuid().ToString("D"), "CEO"));
        var item = PersonalItem(
            "Build the high-level game design document",
            CreativeDirectorAgenda.VisionCorrelation(conversationId), conversationId);

        var result = await new VideoGameCreativeDirectorAgent().HandlePersonalTodoAsync(
            item, context, CancellationToken.None);

        Assert.Equal(PersonalTodoResult.Completed(
            $"Accepted high-level GDD revision {artifactRevisionId:D} ({hash})."), result);
    }

    [Fact]
    public async Task UnknownPersonalCardIsBlockedWithActionableReason()
    {
        var context = new AgentTestRuntime().CreateContext();
        var item = PersonalItem("Unknown", "unknown.v1", Guid.NewGuid());

        var result = await new VideoGameCreativeDirectorAgent().HandlePersonalTodoAsync(
            item, context, CancellationToken.None);

        Assert.Equal(PersonalTodoResult.Blocked(
            "Personal agenda correlation 'unknown.v1' is not supported by this Creative Director version."), result);
    }

    [Fact]
    public async Task AttentionReviewEnsuresOneCorrelatedAgendaCardPerProject()
    {
        var conversationId = Guid.NewGuid();
        var stateKey = VideoGameCreativeDirectorAgent.ProjectStateKey(null, conversationId);
        var state = new CreativeDirectorOperatingState
        {
            IntakeConversationId = conversationId,
            WorkingTitle = "Clockwork Tides",
            Phase = CreativeDirectorPhase.Discovery
        };
        var portfolio = new CreativeDirectorPortfolioIndex
        {
            Projects = [new CreativeDirectorPortfolioEntry(
                stateKey, null, conversationId, null, null, state.WorkingTitle,
                state.Phase, DateTimeOffset.UtcNow)]
        };
        AddPersonalTodoItemRequest? captured = null;
        var runtime = new AgentTestRuntime()
            .RegisterCapability<AgentOperatingStateReadRequest, AgentOperatingStateReadResponse>(
                PlatformCapabilities.AgentOperatingStateRead,
                (request, _) => Task.FromResult(new AgentOperatingStateReadResponse(
                    request.StateKey == VideoGameCreativeDirectorAgent.PortfolioStateKey
                        ? OperatingState(request.StateKey, portfolio)
                        : request.StateKey == stateKey
                            ? OperatingState(request.StateKey, state)
                            : null)))
            .RegisterCapability<AddPersonalTodoItemRequest, PersonalTodoItem>(
                PersonalTodoCapabilities.Add,
                (request, _) =>
                {
                    captured = request;
                    return Task.FromResult(PersonalItem(
                        request.Title, request.CorrelationId!, conversationId));
                });

        await new VideoGameCreativeDirectorAgent().HandleAttentionReviewAsync(
            new AgentAttentionReviewContext(
                Guid.NewGuid(), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(5), "test"),
            runtime.CreateContext(), CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal($"creative-project-review:{conversationId:N}", captured.IdempotencyKey);
        Assert.Equal(CreativeDirectorAgenda.ProjectReviewCorrelation(conversationId), captured.CorrelationId);
        Assert.Equal(conversationId, captured.SourceConversationId);
    }

    [Fact]
    public void PitchReviewUsesTheDocumentAsItsSingleDecisionSurface()
    {
        var message = VideoGameCreativeDirectorAgent.BuildPitchReviewMessage(1);
        Assert.Contains("Open the attached document to review it", message);
        Assert.Contains("More menu for quick approval", message);
        Assert.Contains("wait for your decision", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("below", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PitchWorkAcknowledgementImmediatelySetsHumanExpectations()
    {
        var runtime = new AgentTestRuntime();
        await using var stream = runtime.CreateContext().CreateTurnStream(
            Guid.NewGuid().ToString("D"), Guid.NewGuid());

        await VideoGameCreativeDirectorAgent.PublishPitchWorkAcknowledgementAsync(
            stream, 1, CancellationToken.None);

        Assert.Equal(
            "Perfect—I can work with that. I’m going to start creating the first draft of the game pitch now.",
            VideoGameCreativeDirectorAgent.BuildPitchWorkAcknowledgement(1));
        Assert.Contains(
            "start revising the game pitch now",
            VideoGameCreativeDirectorAgent.BuildPitchWorkAcknowledgement(2),
            StringComparison.OrdinalIgnoreCase);
        var progress = Assert.Single(runtime.Progress);
        Assert.Equal(AgentTurnStreamKinds.DraftDelta, progress.GetProperty("kind").GetString());
        Assert.Contains("start creating the first draft", progress.GetProperty("delta").GetString());
    }

    [Fact]
    public async Task DocumentApprovalEventLocksTheExactPitchAndContinuesTheAgenda()
    {
        var organizationId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var artifactId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();
        var staffingRequestId = Guid.NewGuid();
        var sourceTurnId = Guid.NewGuid();
        var sourceMessageId = Guid.NewGuid();
        const string hash = "exact-revision-hash";
        var stateKey = VideoGameCreativeDirectorAgent.ProjectStateKey(null, conversationId);
        var state = new CreativeDirectorOperatingState
        {
            IntakeConversationId = conversationId,
            Phase = CreativeDirectorPhase.HighLevelReview,
            HighLevelArtifactId = artifactId,
            HighLevelLatestRevisionId = revisionId,
            HighLevelSourceTurnId = sourceTurnId,
            HighLevelSourceMessageId = sourceMessageId,
            StaffingRequestId = staffingRequestId,
            Proposals = [new GamePitchRevision(1, "# Pitch", "pitch-digest", DateTimeOffset.UtcNow, [], [])]
        };
        var portfolio = new CreativeDirectorPortfolioIndex
        {
            Projects = [new CreativeDirectorPortfolioEntry(
                stateKey, null, conversationId, null, null, "Pitch",
                CreativeDirectorPhase.HighLevelReview, DateTimeOffset.UtcNow)]
        };
        var stored = new Dictionary<string, AgentOperatingStateResponse>(StringComparer.Ordinal)
        {
            [stateKey] = OperatingState(stateKey, state),
            [VideoGameCreativeDirectorAgent.PortfolioStateKey] = OperatingState(
                VideoGameCreativeDirectorAgent.PortfolioStateKey, portfolio)
        };
        var confirmations = new List<string>();
        var runtime = new AgentTestRuntime()
            .RegisterCapability<AgentOperatingStateReadRequest, AgentOperatingStateReadResponse>(
                PlatformCapabilities.AgentOperatingStateRead,
                (request, _) => Task.FromResult(new AgentOperatingStateReadResponse(
                    stored.GetValueOrDefault(request.StateKey))))
            .RegisterCapability<AgentOperatingStateWriteRequest, AgentOperatingStateResponse>(
                PlatformCapabilities.AgentOperatingStateWrite,
                (request, _) =>
                {
                    var saved = new AgentOperatingStateResponse(
                        Guid.NewGuid(), request.StateKey, request.SchemaId, request.SchemaVersion,
                        request.Status, request.SourceRevisions, request.ConditionCodes,
                        request.DecisionFingerprint, request.OpenCommitmentCorrelations,
                        request.AttentionReviewId, request.Payload,
                        stored.GetValueOrDefault(request.StateKey)?.Revision + 1 ?? 1,
                        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
                    stored[request.StateKey] = saved;
                    return Task.FromResult(saved);
                })
            .RegisterCapability<JsonElement, ArtifactDocument>(
                PlatformCapabilities.ArtifactRead,
                (_, _) => Task.FromResult(new ArtifactDocument(
                    artifactId, "Game pitch", "video-game.game-vision.v1", "Approved",
                    revisionId, null, revisionId, conversationId, null,
                    [new ArtifactRevision(revisionId, 1, null, "# Pitch", hash, "Accepted",
                        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)])))
            .RegisterCapability<JsonElement, CommunicationMessage>(
                CommunicationCapabilities.MessageSend,
                (request, _) =>
                {
                    confirmations.Add(request.GetProperty("content").GetString()!);
                    return Task.FromResult(new CommunicationMessage(
                        Guid.NewGuid(), 1, conversationId, employeeId, "Creative Director", "Agent",
                        confirmations[^1], DateTimeOffset.UtcNow));
                })
            .RegisterCapability<ResourceChangeReadRequest, ResourceChangeReadResponse>(
                PlatformCapabilities.ResourceChangeRead,
                (_, _) => Task.FromResult(new ResourceChangeReadResponse([])));
        var context = runtime.CreateContext(
            organizationId.ToString("D"), Guid.NewGuid().ToString("D"),
            new AgentIdentity(employeeId.ToString("D"), "Creative Director", null,
                "Creative Director", null, [], null, managerId.ToString("D"), "CEO"));
        var eventId = Guid.NewGuid();
        var resourceEvent = new GenericResourceEvent(
            Guid.NewGuid(), DateTimeOffset.UtcNow,
            new AgentWorkContext(organizationId, Guid.Empty, null, null, null, null, null,
                eventId, null, null),
            "ArtifactRevision", revisionId, 1, "video-game.game-vision.v1", "accepted",
            JsonSerializer.SerializeToElement(new { artifactId, originConversationId = conversationId }));

        await new VideoGameCreativeDirectorAgent().HandleEventAsync(
            new AgentEventEnvelope(Guid.NewGuid(), eventId,
                WorkstreamEventNames.ArtifactRevisionDecidedV1,
                JsonSerializer.SerializeToElement(resourceEvent), DateTimeOffset.UtcNow),
            context, CancellationToken.None);

        var savedState = stored[stateKey].Payload.Deserialize<CreativeDirectorOperatingState>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(savedState?.AcceptedVision);
        Assert.Equal(CreativeDirectorPhase.HighLevelAccepted, savedState!.Phase);
        Assert.Equal(revisionId, savedState.AcceptedVision!.ArtifactRevisionId);
        Assert.Equal(hash, savedState.AcceptedVision.ArtifactRevisionHash);
        Assert.Equal(sourceTurnId, savedState.AcceptedVision.ChatTurnId);
        Assert.Equal(sourceMessageId, savedState.AcceptedVision.MessageId);
        Assert.Contains(confirmations, message => message.Contains("vision is locked", StringComparison.OrdinalIgnoreCase));
    }

    private static AgentOperatingStateResponse OperatingState<T>(string key, T payload) =>
        new(Guid.NewGuid(), key, "test", 1, "Active", new Dictionary<string, string>(), [],
            "fingerprint", [], Guid.NewGuid(), JsonSerializer.SerializeToElement(payload), 1,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private static PersonalTodoItem PersonalItem(string title, string correlationId, Guid conversationId) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Creative Director",
            title, "Requested action:\nPrepare a bounded response.", PersonalTodoStatuses.Ready,
            WorkPriorities.High, 1024, 1, null, conversationId, Guid.NewGuid(), [], null, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
        {
            CorrelationId = correlationId
        };

    private sealed class ReasoningChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(ChatRole.Assistant,
                [new TextReasoningContent("I should preserve the requested tone and scope.")]);
            yield return new ChatResponseUpdate(ChatRole.Assistant,
                [new TextContent("A focused creative answer.")]);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }
}
