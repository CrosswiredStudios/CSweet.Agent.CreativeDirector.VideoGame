using System.Text.Json;
using CSweet.Agent.SDK;
using CrosswiredStudios.VideoGame.AgentKit;
using CrosswiredStudios.VideoGame.ProductionReview;

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
        var sourceEvidence = input.SourceEvidence is null ? null :
            (await ProductionSources.ReadAsync(input.SourceEvidence, input.WorkstreamId, input.TeamId, context, token)).Sources;
        if (sourceEvidence is not null && (sourceEvidence.Pitch.DocumentId != pitch.Document.Id ||
            sourceEvidence.Pitch.RevisionId != pitch.Revision.Id || sourceEvidence.Pitch.Sha256 != pitch.Revision.ContentSha256 ||
            sourceEvidence.Pitch.Content != pitch.Revision.Content))
            return AgentCoordinationTurnResult.Blocked("Production source evidence does not match the exact approved pitch.");
        var ledgerKey = $"creative-review-ledger:{document.Id:N}:{input.PitchSha256}";
        var priorStatus = await context.Platform.ReadOperatingStateAsync<CreativeReviewStatus>(ledgerKey, token);
        var prior = priorStatus?.Payload is { } status && status.DocumentId == document.Id && status.PitchSha256 == input.PitchSha256 &&
            status.Review.ChecklistVersion == CreativeChecklist.Version && document.Revisions.Any(x => x.Id == status.RevisionId &&
                x.ContentSha256 == status.Sha256 && x.Number < revision.Number) ? status.Review : null;
        var key = $"creative-review:{input.WorkstreamId:N}:{input.RevisionId:N}:{input.PitchRevisionId:N}";
        var stored = await context.Platform.ReadOperatingStateAsync<CreativeReviewResult>(key, token);
        if (stored is null)
        {
            var text = await ModelAsync(context, "creative-review", CreativePrompt + """

                Independently assess the supplied exact evidence against the approved player promise and creative pillars.
                Return JSON only: {disposition, summary, findings:[string], requiredChanges:[string], requiresDirector:boolean}.
                disposition is accepted, changes-required, or escalate. Changes-required may include requiresDirector=true
                when concrete rework also needs the Director to resolve a boundary question; preserve both the findings and required changes. Use accepted only with concrete supporting evidence,
                no open blocking issues and no requiredChanges. Findings may retain positive or advisory observations;
                an unresolved blocking issue requires changes-required or escalate. For missing evidence, name what is needed; do not invent playtests.
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

                Complete this fixed checklist in every review: scope, player-loop, controls-accessibility,
                presentation-audio, validation-plan, constraints-non-goals. Return checklistVersion="production-brief.v1",
                checklist:[{id,status,evidence}] with one entry for every ID. Status is passed, blocking, advisory,
                or not-applicable. Assess the whole checklist on the first pass; on revision check the changed
                content and previous findings. Do not reopen a recorded resolution without a cited conflict.
                Return issues:[{id,checkId,severity,status,requirement,evidence,owner,milestone,change}].
                IDs persist across revisions. Severity is blocking or advisory; status is open or resolved.
                Requirement cites the approved requirement or exact Director resolution; evidence names the
                current document section and observed conflict, omission, or resolution. Owner is the role
                responsible and milestone is when the requirement must be satisfied. Preserve EVERY previous
                blocking issue with the same ID, checkId and requirement, including its evidenced resolution.
                Never downgrade or silently drop a blocker. A newly discovered blocker must cite an approved
                requirement or a new risk in changed content; explain why it was not raised in the previous pass.
                Planning can assign later tuning/validation to a named role and milestone. Recommendations,
                optional polish and adequately owned future work are advisory and do not block staffing.
                requiredChanges contains exactly the change of every OPEN BLOCKING issue in issue order.
                Advisory-only reviews are accepted with observations. Unresolved blocking issues require
                changes-required or escalate. Do not invent a new acceptance gate or numerical threshold.
                """, JsonSerializer.Serialize(new { purpose = input.Purpose, approvedPitch = pitch.Revision.Content,
                    approvedGdd = sourceEvidence?.Gdd.Content, documentType = document.DocumentType,
                    evidence = ProductionSources.WorkingBrief(revision.Content), previousReview = prior }), 6000, token);
            var start = text.IndexOf('{'); var end = text.LastIndexOf('}');
            var result = start >= 0 && end >= start ? JsonSerializer.Deserialize<CreativeReviewResult>(text[start..(end + 1)], Json) : null;
            if (!ValidReview(result) || result!.ChecklistVersion != CreativeChecklist.Version ||
                !CreativeChecklist.Valid(result.Checklist, result.Issues, prior?.Issues))
                return AgentCoordinationTurnResult.Blocked("Creative review did not return actionable, consistent findings.");
            stored = await new RevisionSafeProjectState(context.Platform).MergeAsync<CreativeReviewResult>(key,
                "video-game.creative-review.v1", 1, prior => prior ?? result!,
                new Dictionary<string, string> { ["evidence"] = input.Sha256, ["pitch"] = input.PitchSha256 }, key, token);
        }
        var currentStatus = new CreativeReviewStatus(input.DocumentId, input.RevisionId, input.Sha256, stored.Payload)
            { PitchSha256 = input.PitchSha256, RevisionNumber = revision.Number };
        var ledgerSources = new Dictionary<string, string>();
        await new RevisionSafeProjectState(context.Platform).MergeAsync<CreativeReviewStatus>(ledgerKey,
            "video-game.creative-review-ledger.v1", 1,
            current => {
                var selected = current is not null && current.RevisionNumber > revision.Number ? current : currentStatus;
                ledgerSources["evidence"] = selected.Sha256;
                return selected;
            }, ledgerSources, key + ":ledger", token);
        var statusSources = new Dictionary<string, string>();
        await new RevisionSafeProjectState(context.Platform).MergeAsync<CreativeReviewStatus>(
            $"creative-review-status:{input.WorkstreamId:N}", "video-game.creative-review-status.v1", 1,
            current => {
                var selected = current is not null && current.DocumentId == document.Id && current.PitchSha256 == input.PitchSha256 &&
                    current.RevisionNumber > revision.Number ? current : currentStatus;
                statusSources["evidence"] = selected.Sha256;
                return selected;
            }, statusSources, key + ":status", token);
        return AgentCoordinationTurnResult.Continue(stored.Payload.Summary,
            new(CreativeReviewResultType, "1.0", input.Sha256, 1, true,
                JsonSerializer.SerializeToElement(new { request = input, review = stored.Payload }, Json)));
    }

    internal static bool ValidReview(CreativeReviewResult? review) => review is not null &&
        !string.IsNullOrWhiteSpace(review.Summary) && review.Findings is not null && review.RequiredChanges is not null &&
        review.Findings.All(x => !string.IsNullOrWhiteSpace(x)) && review.RequiredChanges.All(x => !string.IsNullOrWhiteSpace(x)) &&
        (review.ChecklistVersion is null || review.ChecklistVersion == CreativeChecklist.Version &&
            CreativeChecklist.Valid(review.Checklist, review.Issues) && CreativeChecklist.MatchesDisposition(review.Disposition, review.RequiresDirector, review.RequiredChanges, review.Issues)) &&
        (review.Disposition == "accepted" ? !review.RequiresDirector && review.RequiredChanges.Count == 0 :
         review.Disposition == "changes-required" ? review.RequiredChanges.Count > 0 && (!review.RequiresDirector || review.Findings.Count > 0) :
         review.Disposition == "escalate" && review.RequiresDirector && review.Findings.Count > 0);
}

internal sealed record CreativeReviewRequest(Guid WorkstreamId, Guid TeamId, Guid PitchArtifactId, Guid PitchRevisionId,
    string PitchSha256, Guid DocumentId, Guid RevisionId, string Sha256, string Purpose, Guid? ParentSessionId = null)
{
    public CollaborationDocumentReference? SourceEvidence { get; init; }
}
internal sealed record CreativeReviewResult(string Disposition, string Summary, IReadOnlyList<string> Findings,
    IReadOnlyList<string> RequiredChanges, bool RequiresDirector)
{
    public string? ChecklistVersion { get; init; }
    public IReadOnlyList<CreativeCheck> Checklist { get; init; } = [];
    public IReadOnlyList<CreativeIssue> Issues { get; init; } = [];
}
internal sealed record CreativeReviewStatus(Guid DocumentId, Guid RevisionId, string Sha256, CreativeReviewResult Review)
{
    public string? PitchSha256 { get; init; }
    public int RevisionNumber { get; init; }
}
