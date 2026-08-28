using System.Text.Json;
using CSweet.Agent.SDK;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class CreativeDirectorOnboardingTests
{
    [Fact]
    public async Task OnboardingIsIdempotentAndCompletesLifecycleWithoutStaffing()
    {
        var organizationId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        AgentOperatingStateResponse? stored = null;
        var messages = 0;
        var completions = 0;
        var staffingProposals = 0;
        var runtime = new AgentTestRuntime()
            .RegisterCapability<AgentOperatingStateReadRequest, AgentOperatingStateReadResponse>(
                PlatformCapabilities.AgentOperatingStateRead,
                (_, _) => Task.FromResult(new AgentOperatingStateReadResponse(stored)))
            .RegisterCapability<AgentOperatingStateWriteRequest, AgentOperatingStateResponse>(
                PlatformCapabilities.AgentOperatingStateWrite,
                (request, _) =>
                {
                    stored = new AgentOperatingStateResponse(
                        Guid.NewGuid(), request.StateKey, request.SchemaId, request.SchemaVersion,
                        request.Status, request.SourceRevisions, request.ConditionCodes,
                        request.DecisionFingerprint, request.OpenCommitmentCorrelations,
                        request.AttentionReviewId, request.Payload, (stored?.Revision ?? 0) + 1,
                        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
                    return Task.FromResult(stored);
                })
            .RegisterCapability<JsonElement, CommunicationMessage>(
                CommunicationCapabilities.MessageSend,
                (request, _) =>
                {
                    messages++;
                    return Task.FromResult(new CommunicationMessage(
                        Guid.NewGuid(), messages, request.GetProperty("chatId").GetGuid(), employeeId,
                        "Creative Director", "Agent", request.GetProperty("content").GetString()!,
                        DateTimeOffset.UtcNow));
                })
            .RegisterCapability<CompleteAgentOnboardingRequest, CompleteAgentOnboardingResponse>(
                AgentLifecycleCapabilities.CompleteOnboarding,
                (_, _) =>
                {
                    completions++;
                    return Task.FromResult(new CompleteAgentOnboardingResponse(true, DateTimeOffset.UtcNow));
                })
            .RegisterCapability<ResourceChangeProposalRequest, ResourceChangeRequestResponse>(
                PlatformCapabilities.ResourceChangePropose,
                (_, _) =>
                {
                    staffingProposals++;
                    throw new InvalidOperationException("Onboarding must not submit staffing.");
                });
        var context = runtime.CreateContext(
            organizationId.ToString("D"), Guid.NewGuid().ToString("D"),
            new AgentIdentity(employeeId.ToString("D"), "Creative Director", null,
                "Creative Director", null, [], null, managerId.ToString("D"), "Owner"));
        var envelope = new AgentEventEnvelope(
            Guid.NewGuid(), eventId, AgentLifecycleEvents.Onboarded,
            JsonSerializer.SerializeToElement(new AgentOnboardedEvent(
                organizationId, employeeId, managerId, conversationId, DateTimeOffset.UtcNow)),
            DateTimeOffset.UtcNow);
        var agent = new VideoGameCreativeDirectorAgent();

        await agent.HandleEventAsync(envelope, context, CancellationToken.None);
        await agent.HandleEventAsync(envelope, context, CancellationToken.None);

        Assert.Equal(1, messages);
        Assert.Equal(2, completions);
        Assert.Equal(0, staffingProposals);
        var state = stored!.Payload.Deserialize<CreativeDirectorOperatingState>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(eventId, state!.OnboardingEventId);
        Assert.True(state.IntakeChoiceAsked);
    }
}
