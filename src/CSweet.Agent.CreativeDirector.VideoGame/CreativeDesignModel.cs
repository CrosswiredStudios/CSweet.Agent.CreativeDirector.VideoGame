using CSweet.Agent.SDK;
using CSweet.Agent.SDK.VideoGame.Contracts;
using CrosswiredStudios.VideoGame.AgentKit;
using Microsoft.Extensions.AI;

namespace CSweet.Agent.CreativeDirector.VideoGame;

public sealed partial class VideoGameCreativeDirectorAgent
{
    private sealed record DesignGeneration(string Markdown, bool Incomplete);

    private async Task<(string? Markdown, GameCreativeDesignFailureV1? Failure)> GenerateDesignAsync(AgentCoordinationTurnRequest request,
        string draftKey, GameCreativeDesignRequestV1 assignment, string input, AgentRuntimeContext context, CancellationToken token)
    {
        var system = CreativePrompt + "\nReturn the complete GDD, covering creative intent, player experience, gameplay, " +
            "presentation, planned acceptance methods, approved constraints and unresolved decisions. " +
            "Organize it in any clear structure: headings, tables or prose. No exact heading names, levels or order are required. " +
            "Keep unanswered questions explicit; never invent CEO approval. Preserve the approved pitch and address the supplied " +
            "Director findings. This is a draft for independent Director review, not a claim that its content has been accepted.";
        var (client, instructions) = CreateModel(context, "creative-design", system);
        var messages = new List<ChatMessage> { new(ChatRole.System, instructions), new(ChatRole.User, input) };
        for (var attempt = 0; ; attempt++)
        {
            // Persist generation evidence separately from the publication draft. Delivery replay reuses
            // both attempts; an explicit session resume or package update can start a fresh bounded pass.
            var key = AgentOperatingStateKey.Create("creative-design-generation.v1",
                new { draftKey, request.SessionId, request.ExpectedRevision, Version, attempt });
            var stored = await context.Platform.ReadOperatingStateAsync<DesignGeneration>(key, token);
            if (stored is null)
            {
                var response = await client.GetResponseAsync(messages, new ChatOptions { MaxOutputTokens = 12000 }, token);
                var generation = new DesignGeneration(response.Text.Trim(), response.FinishReason == ChatFinishReason.Length);
                stored = await new RevisionSafeProjectState(context.Platform).MergeAsync<DesignGeneration>(key,
                    "video-game.creative-design.generation.v1", 1, prior => prior ?? generation,
                    new Dictionary<string, string> { ["pitch"] = assignment.PitchSha256 }, key, token);
            }
            var text = stored.Payload.Markdown;
            var error = stored.Payload.Incomplete ? "The response reached its output limit; return a shorter complete GDD."
                : string.IsNullOrWhiteSpace(text) ? "The response contained no document content." : null;
            if (error is null) return (text, null);
            if (attempt == 1) return (null, new(assignment.WorkstreamId, assignment.TeamId, assignment.PitchSha256,
                stored.Payload.Incomplete ? GameCreativeDesignFailureCodes.IncompleteGeneration : GameCreativeDesignFailureCodes.EmptyGeneration, error));
            messages.Add(new(ChatRole.Assistant, text.Length > 24000 ? text[..24000] : text));
            messages.Add(new(ChatRole.User, "GDD generation could not finish: " + error +
                " Return the full corrected GDD in any clear structure. Preserve the approved pitch, prior GDD and Director findings. " +
                "Do not claim approval or omit unresolved questions."));
        }
    }

}
