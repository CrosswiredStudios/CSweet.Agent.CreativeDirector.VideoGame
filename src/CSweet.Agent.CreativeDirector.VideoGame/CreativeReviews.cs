using System.Text.Json;
using CSweet.Agent.SDK;
using CrosswiredStudios.VideoGame.AgentKit;

namespace CSweet.Agent.CreativeDirector.VideoGame;

public sealed partial class VideoGameCreativeDirectorAgent
{
    internal const string CreativeReviewRequestType = "video-game.creative-review.request.v1";
    internal const string CreativeReviewResultType = "video-game.creative-review.result.v1";

    // Transport, replay, access grants and completion delivery use SDK coordination and artifact primitives.
    private async Task<AgentCoordinationTurnResult> ReviewCreativeEvidenceAsync(
        AgentCoordinationTurnRequest request, AgentRuntimeContext context, CancellationToken token)
    {
        var source = request.Transcript.FirstOrDefault(x => x.SpeakerOrganizationUserId == request.Counterpart.OrganizationUserId &&
            x.Artifact?.Type == CreativeReviewRequestType)?.Artifact;
        var input = source?.Payload.Deserialize<CreativeReviewRequest>(Json);
        if (input is null || !Guid.TryParse(context.Identity?.ManagerEmployeeId, out var manager) ||
            manager != request.Counterpart.OrganizationUserId || request.WorkContext?.WorkstreamId != input.WorkstreamId ||
            request.WorkContext.TeamId != input.TeamId || source!.Key != input.Sha256)
            return AgentCoordinationTurnResult.Blocked("Creative review requires the Director and exact project, team and evidence revision.");
        var pitch = await context.Platform.Artifacts.ReadAcceptedAsync(new(input.PitchArtifactId, input.PitchRevisionId, input.PitchSha256), token);
        var document = await context.Platform.Artifacts.GetAsync(input.DocumentId, token);
        var revision = document.Revisions.SingleOrDefault(x => x.Id == input.RevisionId && x.ContentSha256 == input.Sha256);
        if (revision is null || document.WorkstreamId != input.WorkstreamId || document.TeamId != input.TeamId)
            return AgentCoordinationTurnResult.Blocked("Creative evidence is stale or belongs to another project.");
        var own = await context.Platform.ReadOperatingStateAsync<CreativeProjectEvidence>($"creative-project:{input.WorkstreamId:N}", token);
        if (own?.Payload.DocumentId == document.Id)
            return AgentCoordinationTurnResult.Blocked("My own GDD requires independent Director review; I cannot approve my own work.");
        var key = $"creative-review:{input.WorkstreamId:N}:{input.RevisionId:N}:{input.PitchRevisionId:N}";
        var stored = await context.Platform.ReadOperatingStateAsync<CreativeReviewResult>(key, token);
        if (stored is null)
        {
            var text = await ModelAsync(context, "creative-review", CreativePrompt + """

                Independently assess the supplied exact evidence against the approved player promise and creative pillars.
                Return JSON only: {disposition, summary, findings:[string], requiredChanges:[string], requiresDirector:boolean}.
                disposition is accepted, changes-required, or escalate. Use accepted only with concrete supporting evidence,
                no unresolved findings and no requiredChanges. For missing evidence, name what is needed; do not invent playtests.
                Escalate proposed changes to approved scope, platforms or budget. Technical truth and QA remain with their owners.
                Review creative intent and consistency, never certify implementation, performance or launch readiness.
                Match evidence requirements to the document's lifecycle stage. For a production plan, production
                brief, GDD or other pre-production planning document, concrete supporting evidence means exact
                approved scope, design decisions, planned acceptance methods, owners and future validation tasks.
                Never require a playable build, completed playtests, recorded sessions or observed implementation
                before staffing or planning. Require those observations when reviewing an implemented playable
                milestone, and never represent a planning acceptance as acceptance of the finished game.
                Resolve creative details within approved scope yourself and give concrete recommendations.
                RequiresDirector means your manager must resolve a scope or authority question; it does not
                mean the CEO must decide, and routine validation methods are planned specialist work.
                """, JsonSerializer.Serialize(new { purpose = input.Purpose, approvedPitch = pitch.Revision.Content,
                    documentType = document.DocumentType, evidence = revision.Content }), 4000, token);
            var start = text.IndexOf('{'); var end = text.LastIndexOf('}');
            var result = start >= 0 && end >= start ? JsonSerializer.Deserialize<CreativeReviewResult>(text[start..(end + 1)], Json) : null;
            if (!ValidReview(result)) return AgentCoordinationTurnResult.Blocked("Creative review did not return actionable, consistent findings.");
            stored = await new RevisionSafeProjectState(context.Platform).MergeAsync<CreativeReviewResult>(key,
                "video-game.creative-review.v1", 1, prior => prior ?? result!,
                new Dictionary<string, string> { ["evidence"] = input.Sha256, ["pitch"] = input.PitchSha256 }, key, token);
        }
        await new RevisionSafeProjectState(context.Platform).MergeAsync<CreativeReviewStatus>(
            $"creative-review-status:{input.WorkstreamId:N}", "video-game.creative-review-status.v1", 1,
            _ => new(input.DocumentId, input.RevisionId, input.Sha256, stored.Payload),
            new Dictionary<string, string> { ["evidence"] = input.Sha256 }, key + ":status", token);
        return AgentCoordinationTurnResult.Continue(stored.Payload.Summary,
            new(CreativeReviewResultType, "1.0", input.Sha256, 1, true,
                JsonSerializer.SerializeToElement(new { request = input, review = stored.Payload }, Json)));
    }

    internal static bool ValidReview(CreativeReviewResult? review) => review is not null &&
        !string.IsNullOrWhiteSpace(review.Summary) && review.Findings is not null && review.RequiredChanges is not null &&
        review.Findings.All(x => !string.IsNullOrWhiteSpace(x)) && review.RequiredChanges.All(x => !string.IsNullOrWhiteSpace(x)) &&
        (review.Disposition == "accepted" ? !review.RequiresDirector && review.Findings.Count == 0 && review.RequiredChanges.Count == 0 :
         review.Disposition == "changes-required" ? !review.RequiresDirector && review.RequiredChanges.Count > 0 :
         review.Disposition == "escalate" && review.RequiresDirector && review.Findings.Count > 0);
}

internal sealed record CreativeReviewRequest(Guid WorkstreamId, Guid TeamId, Guid PitchArtifactId, Guid PitchRevisionId,
    string PitchSha256, Guid DocumentId, Guid RevisionId, string Sha256, string Purpose, Guid? ParentSessionId = null);
internal sealed record CreativeReviewResult(string Disposition, string Summary, IReadOnlyList<string> Findings,
    IReadOnlyList<string> RequiredChanges, bool RequiresDirector);
internal sealed record CreativeReviewStatus(Guid DocumentId, Guid RevisionId, string Sha256, CreativeReviewResult Review);
