using System.Text.Json;
using CSweet.Agent.SDK;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class OnboardingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Introduction_requires_and_contacts_only_reporting_Director(bool hasManager)
    {
        var org = Guid.NewGuid(); var creative = Guid.NewGuid(); var director = Guid.NewGuid(); var ceo = Guid.NewGuid();
        var chat = Guid.NewGuid(); var eventId = Guid.NewGuid(); var recipients = new List<Guid>(); var completed = false;
        var runtime = new AgentTestRuntime()
            .RegisterCapability<JsonElement, JsonElement>("communication.chat.read.v1", (_, _) => Task.FromResult(JsonSerializer.SerializeToElement(new { chats = Array.Empty<object>() })))
            .RegisterCapability<JsonElement, JsonElement>("communication.chat.create.v1", (r, _) => {
                var recipient = Assert.Single(r.GetProperty("participantOrganizationUserIds").EnumerateArray()).GetGuid();
                recipients.Add(recipient);
                return Task.FromResult(JsonSerializer.SerializeToElement(new { succeeded = true, message = "Created", chat = new {
                    id = chat, isDirect = true, isPrivate = true, participants = new[] { new { organizationUserId = director, employeeType = "Agent", displayName = "Marcus", role = "Member" } } } })); })
            .RegisterCapability<JsonElement, CommunicationMessage>("communication.message.send.v1", (r, _) => {
                Assert.Equal(chat, r.GetProperty("chatId").GetGuid());
                return Task.FromResult(new CommunicationMessage(Guid.NewGuid(), 1, chat, creative, "Naomi", "Agent",
                    r.GetProperty("content").GetString()!, DateTimeOffset.UtcNow, Guid.NewGuid(), null, [])); })
            .RegisterCapability<CompleteAgentOnboardingRequest, CompleteAgentOnboardingResponse>(AgentLifecycleCapabilities.CompleteOnboarding, (_, _) => {
                Assert.Equal(director, Assert.Single(recipients)); completed = true;
                return Task.FromResult(new CompleteAgentOnboardingResponse(true, DateTimeOffset.UtcNow)); });
        var context = runtime.CreateContext(org.ToString(), identity: new(creative.ToString(), "Naomi", null, "Creative Director", null, [], null, hasManager ? director.ToString() : null, "Marcus"));
        var envelope = new AgentEventEnvelope(Guid.NewGuid(), eventId, AgentLifecycleEvents.Onboarded,
            JsonSerializer.SerializeToElement(new AgentOnboardedEvent(org, creative, ceo, Guid.NewGuid(), DateTimeOffset.UtcNow)), DateTimeOffset.UtcNow);
        var agent = new VideoGameCreativeDirectorAgent();
        if (hasManager) await agent.HandleEventAsync(envelope, context, default);
        else await Assert.ThrowsAsync<InvalidOperationException>(() => agent.HandleEventAsync(envelope, context, default));
        Assert.Equal(hasManager, completed); Assert.DoesNotContain(ceo, recipients);
    }
}
