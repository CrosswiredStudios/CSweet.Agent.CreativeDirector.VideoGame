using System.Text.Json;
using CSweet.Agent.SDK;
using CrosswiredStudios.VideoGame.ProductionReview;
using Microsoft.Extensions.AI;

namespace CSweet.Agent.CreativeDirector.VideoGame;

/// <summary>The model's review could not be turned into a consistent review; the message says exactly why.</summary>
internal sealed class CreativeReviewContractException(string message) : InvalidOperationException(message);

public sealed partial class VideoGameCreativeDirectorAgent
{
    // The model supplies the judgment (checks, issues, findings, escalation). Code derives what follows from
    // it, and only a real contradiction goes back to the same conversation with the exact reason, so a
    // correction repairs this review instead of producing a different one.
    private async Task<CreativeReviewResult> ReviewModelAsync(AgentRuntimeContext context, string system, string input,
        IReadOnlyList<CreativeIssue>? previous, CancellationToken token)
    {
        var (client, instructions) = CreateModel(context, "creative-review", system);
        ChatMessage[] original = [new(ChatRole.System, instructions), new(ChatRole.User, input)];
        var messages = new List<ChatMessage>(original);
        var options = new ChatOptions
        {
            MaxOutputTokens = CreativeReviewModel.MaxOutputTokens,
            ResponseFormat = ChatResponseFormat.ForJsonSchema(CreativeReviewModel.Schema, CreativeReviewModel.SchemaName)
        };
        for (var attempt = 0; ; attempt++)
        {
            var response = await client.GetResponseAsync(messages, options, token);
            var text = response.Text.Trim();
            try
            {
                if (response.FinishReason == ChatFinishReason.Length)
                    throw new CreativeReviewContractException(
                        "the response ran out of output budget before the JSON object was complete; keep evidence and findings shorter");
                return CreativeReviewModel.Normalize(CreativeReviewModel.Parse(text), previous);
            }
            catch (CreativeReviewContractException error) when (attempt < CreativeReviewModel.CorrectionLimit)
            {
                // Keep only the latest invalid response so corrections do not accumulate whole reviews.
                messages.Clear();
                messages.AddRange(original);
                messages.Add(new(ChatRole.Assistant, text.Length > CreativeReviewModel.MaxEchoCharacters
                    ? text[..CreativeReviewModel.MaxEchoCharacters] : text));
                messages.Add(new(ChatRole.User, CreativeReviewModel.Correction(error.Message, attempt + 1)));
            }
        }
    }
}

internal static class CreativeReviewModel
{
    internal const int CorrectionLimit = 2;
    internal const int MaxOutputTokens = 6000;
    internal const int MaxEchoCharacters = 24000;
    internal const string SchemaName = "creative_review";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal const string Instructions = """

        Independently assess the supplied exact evidence against the approved player promise and creative pillars.
        Return one JSON object only: {disposition, summary, findings:[string], requiresDirector:boolean, checklistVersion,
        checklist:[{id,status,evidence}], issues:[{id,checkId,severity,status,requirement,evidence,owner,milestone,change}]}.
        Your issues decide the outcome: any open blocking issue means changes are required; with none the review is
        accepted. Required changes are taken from the change field of your open blocking issues; do not list them separately.
        disposition is accepted, changes-required, or escalate. Use escalate with requiresDirector=true for proposed changes
        to approved scope, platforms or budget, and record each such question as an open blocking issue. Changes-required
        may include requiresDirector=true when concrete rework also needs the Director to resolve a boundary question;
        preserve both the findings and the issues. Findings may retain positive or advisory observations.
        For missing evidence, name what is needed; do not invent playtests. Technical truth and QA remain with their owners.
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
        presentation-audio, validation-plan, constraints-non-goals. Return checklistVersion="production-brief.v1"
        and one checklist entry for every ID. A check with an open blocking issue is blocking, a check with only
        open advisory issues is advisory, and a check with no open issue is passed or not-applicable.
        Assess the whole checklist on the first pass; on revision check the changed
        content and previous findings. Do not reopen a recorded resolution without a cited conflict.
        IDs persist across revisions. Severity is blocking or advisory; status is open or resolved.
        Requirement cites the approved requirement or exact Director resolution; evidence names the
        current document section and observed conflict, omission, or resolution. Owner is the role
        responsible and milestone is when the requirement must be satisfied. Preserve EVERY previous
        blocking issue with the same ID, checkId and requirement, including its evidenced resolution.
        Never downgrade or silently drop a blocker. A newly discovered blocker must cite an approved
        requirement or a new risk in changed content; explain why it was not raised in the previous pass.
        Planning can assign later tuning/validation to a named role and milestone. Recommendations,
        optional polish and adequately owned future work are advisory and do not block staffing.
        Do not invent a new acceptance gate or numerical threshold.
        """;

    // Hand-written so every property is required and closed, which strict structured-output providers need.
    internal static readonly JsonElement Schema = JsonSerializer.SerializeToElement(new Dictionary<string, object>
    {
        ["type"] = "object",
        ["additionalProperties"] = false,
        ["required"] = new[] { "disposition", "summary", "findings", "requiresDirector", "checklistVersion", "checklist", "issues" },
        ["properties"] = new Dictionary<string, object>
        {
            ["disposition"] = OneOf("accepted", "changes-required", "escalate"),
            ["summary"] = Text,
            ["findings"] = new Dictionary<string, object> { ["type"] = "array", ["items"] = Text },
            ["requiresDirector"] = new Dictionary<string, object> { ["type"] = "boolean" },
            ["checklistVersion"] = OneOf(CreativeChecklist.Version),
            ["checklist"] = ArrayOf(new()
            {
                ["id"] = OneOf(CreativeChecklist.CheckIds),
                ["status"] = OneOf("passed", "blocking", "advisory", "not-applicable"),
                ["evidence"] = Text
            }),
            ["issues"] = ArrayOf(new()
            {
                ["id"] = Text,
                ["checkId"] = OneOf(CreativeChecklist.CheckIds),
                ["severity"] = OneOf("blocking", "advisory"),
                ["status"] = OneOf("open", "resolved"),
                ["requirement"] = Text,
                ["evidence"] = Text,
                ["owner"] = Text,
                ["milestone"] = Text,
                ["change"] = Text
            })
        }
    });

    private static Dictionary<string, object> Text => new() { ["type"] = "string" };
    private static Dictionary<string, object> OneOf(params string[] values) => new() { ["type"] = "string", ["enum"] = values };
    private static Dictionary<string, object> ArrayOf(Dictionary<string, object> properties) => new()
    {
        ["type"] = "array",
        ["items"] = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = properties.Keys.ToArray(),
            ["properties"] = properties
        }
    };

    internal static string Correction(string problem, int correction) =>
        $"Your creative review could not be accepted: {problem}. Correction {correction}/{CorrectionLimit}: return the complete " +
        "review again as one JSON object with exactly these problems fixed. Keep your assessment, findings and issues otherwise " +
        "unchanged; do not drop, weaken or invent an issue to satisfy validation.";

    internal static CreativeReviewResult Parse(string text)
    {
        // Structured output normally returns bare JSON; a provider without it may still wrap the object in prose or fences.
        var start = text.IndexOf('{'); var end = text.LastIndexOf('}');
        if (start < 0 || end <= start) throw new CreativeReviewContractException("the response contained no JSON object");
        try
        {
            return JsonSerializer.Deserialize<CreativeReviewResult>(text[start..(end + 1)], Json)
                ?? throw new CreativeReviewContractException("the response JSON was null");
        }
        catch (JsonException error)
        {
            throw new CreativeReviewContractException($"the response is not valid review JSON ({error.Message})");
        }
    }

    /// <summary>
    /// Derives each check's status, the required changes and the disposition from the model's own issues, and
    /// rejects only contradictions code cannot settle: missing or unknown checks, incomplete issues, a check
    /// marked blocking without a blocking issue, an escalation without a blocking question, or an earlier
    /// blocker that was dropped, moved, reworded or downgraded.
    /// </summary>
    internal static CreativeReviewResult Normalize(CreativeReviewResult review, IReadOnlyList<CreativeIssue>? previous)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(review.Summary)) problems.Add("summary is empty");
        var checks = (review.Checklist ?? []).Where(x => x is not null).ToArray();
        var issues = (review.Issues ?? []).Where(x => x is not null).ToArray();

        var missing = CreativeChecklist.CheckIds.Where(id => !checks.Any(x => x.Id == id)).ToArray();
        if (missing.Length > 0) problems.Add($"checklist is missing {string.Join(", ", missing)}");
        var unknown = checks.Select(x => x.Id).Where(id => !CreativeChecklist.CheckIds.Contains(id)).Distinct().ToArray();
        if (unknown.Length > 0) problems.Add($"checklist has unknown ids {string.Join(", ", unknown.Select(x => $"\"{x}\""))}");
        var repeated = checks.GroupBy(x => x.Id).Where(x => x.Count() > 1).Select(x => x.Key).ToArray();
        if (repeated.Length > 0) problems.Add($"checklist repeats {string.Join(", ", repeated)}");
        foreach (var check in checks.Where(x => string.IsNullOrWhiteSpace(x.Evidence)))
            problems.Add($"checklist entry {check.Id} has no evidence");

        if (issues.Length > 40) problems.Add($"there are {issues.Length} issues; merge them to at most 40");
        for (var i = 0; i < issues.Length; i++)
        {
            var issue = issues[i];
            var label = string.IsNullOrWhiteSpace(issue.Id) ? $"issue #{i + 1}" : $"issue {issue.Id}";
            if (string.IsNullOrWhiteSpace(issue.Id) || issue.Id.Length > 80) problems.Add($"{label} needs an id of at most 80 characters");
            if (!CreativeChecklist.CheckIds.Contains(issue.CheckId)) problems.Add($"{label} has unknown checkId \"{issue.CheckId}\"");
            if (issue.Severity is not ("blocking" or "advisory")) problems.Add($"{label} severity must be blocking or advisory");
            if (issue.Status is not ("open" or "resolved")) problems.Add($"{label} status must be open or resolved");
            var empty = new[] { ("requirement", issue.Requirement), ("evidence", issue.Evidence), ("owner", issue.Owner),
                ("milestone", issue.Milestone), ("change", issue.Change) }.Where(x => string.IsNullOrWhiteSpace(x.Item2)).Select(x => x.Item1).ToArray();
            if (empty.Length > 0) problems.Add($"{label} is missing {string.Join(", ", empty)}");
        }
        var duplicateIssues = issues.Where(x => !string.IsNullOrWhiteSpace(x.Id)).GroupBy(x => x.Id, StringComparer.Ordinal)
            .Where(x => x.Count() > 1).Select(x => x.Key).ToArray();
        if (duplicateIssues.Length > 0) problems.Add($"issue ids repeat: {string.Join(", ", duplicateIssues)}");

        foreach (var prior in (previous ?? []).Where(x => x.Severity == "blocking"))
        {
            var carried = issues.FirstOrDefault(x => x.Id == prior.Id);
            if (carried is null)
                problems.Add($"earlier blocking issue {prior.Id} is missing; keep it with the same id, checkId and requirement, " +
                    "either still open or resolved with evidence");
            else if (carried.CheckId != prior.CheckId || carried.Requirement != prior.Requirement || carried.Severity != "blocking")
                problems.Add($"earlier blocking issue {prior.Id} must keep checkId \"{prior.CheckId}\", its exact requirement and " +
                    "severity blocking; mark it resolved with evidence instead of changing it");
        }

        var normalizedChecks = new List<CreativeCheck>();
        foreach (var id in CreativeChecklist.CheckIds)
        {
            var check = checks.FirstOrDefault(x => x.Id == id);
            if (check is null) continue;
            var open = issues.Where(x => x.CheckId == id && x.Status == "open").ToArray();
            string? status = open.Any(x => x.Severity == "blocking") ? "blocking" : open.Length > 0 ? "advisory" : check.Status switch
            {
                "passed" or "not-applicable" => check.Status,
                "advisory" => "passed", // An advisory mark with no advisory issue carries nothing to act on.
                "blocking" => null,
                _ => null
            };
            if (status is null)
                problems.Add(check.Status == "blocking"
                    ? $"checklist entry {id} is blocking but has no open blocking issue; add that issue with its change, or mark the check passed"
                    : $"checklist entry {id} status must be passed, blocking, advisory or not-applicable");
            else normalizedChecks.Add(check with { Status = status });
        }

        var blockers = CreativeChecklist.Blockers(issues);
        var escalation = review.RequiresDirector || review.Disposition == "escalate";
        if (blockers.Length == 0 && escalation)
            problems.Add("requiresDirector or escalate needs at least one open blocking issue stating the question the Director " +
                "must resolve; otherwise set requiresDirector to false");
        if (problems.Count > 0) throw new CreativeReviewContractException(string.Join("; ", problems));

        var disposition = blockers.Length == 0 ? "accepted" : review.Disposition == "escalate" ? "escalate" : "changes-required";
        var requiresDirector = blockers.Length > 0 && escalation;
        var findings = (review.Findings ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        if (findings.Count == 0 && requiresDirector) findings.Add(review.Summary);
        var result = new CreativeReviewResult(disposition, review.Summary, findings, blockers.Select(x => x.Change).ToArray(), requiresDirector)
        {
            ChecklistVersion = CreativeChecklist.Version,
            Checklist = normalizedChecks,
            Issues = issues
        };
        // Holds by construction; kept so the Director, which applies the same rules, never receives a review it would reject.
        if (!VideoGameCreativeDirectorAgent.ValidReview(result) || !CreativeChecklist.Valid(result.Checklist, result.Issues, previous))
            throw new CreativeReviewContractException("the review is internally inconsistent after normalization");
        return result;
    }
}
