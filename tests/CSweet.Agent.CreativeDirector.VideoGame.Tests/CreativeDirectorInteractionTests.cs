using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class CreativeDirectorInteractionTests
{
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
}
