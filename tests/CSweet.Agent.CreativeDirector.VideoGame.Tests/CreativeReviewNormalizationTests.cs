using System.Text.Json;
using CrosswiredStudios.VideoGame.ProductionReview;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

// The three production-brief reviews QFN Games' Creative Director returned on 2026-11-11 (19:42, 19:46 and 19:51 UTC).
// Ids, checks, severities and statuses are exact; prose is shortened. Version 4.1.1 rejected all three for
// bookkeeping it can derive, and the Director's session blocked with no staffing proposal.
public sealed class CreativeReviewNormalizationTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static CreativeIssue I(string id, string check, string severity, string change) =>
        new(id, check, severity, "open", $"Approved requirement behind {id}", $"Brief section that omits {id}", "Producer", "pre-production", change);

    private static CreativeReviewResult Recorded(string disposition, string[] requiredChanges, string[] statuses, params CreativeIssue[] issues) =>
        new(disposition, "Recorded QFN Games production-brief review", ["The three creative pillars are preserved"], requiredChanges, false)
        {
            ChecklistVersion = CreativeChecklist.Version,
            Checklist = CreativeChecklist.CheckIds.Zip(statuses, (id, status) => new CreativeCheck(id, status, $"Checked {id} against the brief")).ToArray(),
            Issues = issues
        };

    // Round trip through text, as the model returns it.
    private static CreativeReviewResult Normalize(CreativeReviewResult review, IReadOnlyList<CreativeIssue>? previous = null) =>
        CreativeReviewModel.Normalize(CreativeReviewModel.Parse(JsonSerializer.Serialize(review, Web)), previous);

    private static string[] Statuses(CreativeReviewResult review) => review.Checklist.Select(x => x.Status).ToArray();

    // Checklist order: scope, player-loop, controls-accessibility, presentation-audio, validation-plan, constraints-non-goals.
    internal static CreativeReviewResult Attempt1 => Recorded("changes-required", ["Add an explicit ownership and validation plan"],
        ["passed", "passed", "passed", "advisory", "blocking", "passed"],
        I("pb-validation-owners-v1", "validation-plan", "blocking", "Add an explicit ownership and validation plan"),
        I("pb-visual-identity-v1", "presentation-audio", "advisory", "Reference neon presentation acceptance"),
        I("pb-level-count-v1", "scope", "advisory", "Confirm the level count"),
        I("pb-accessibility-depth-v1", "controls-accessibility", "advisory", "Detail accessibility settings"),
        I("pb-audio-sourcing-v1", "presentation-audio", "advisory", "Name the audio sourcing approach"));

    internal static CreativeReviewResult Attempt2 => Recorded("changes-required",
        ["PB-001: Record that the screen-reader-friendly menu requirement remains in PoC acceptance", "PB-002: Name validation owners"],
        ["advisory", "passed", "blocking", "advisory", "blocking", "advisory"],
        I("PB-001", "controls-accessibility", "blocking", "Record that screen-reader-friendly menu accessibility remains in approved PoC scope"),
        I("PB-002", "validation-plan", "blocking", "Name owners and the planned Chromium observation method"),
        I("PB-003", "scope", "advisory", "Confirm wave count"),
        I("PB-004", "scope", "advisory", "Confirm brick roster"),
        I("PB-005", "presentation-audio", "advisory", "Reference the neon identity"),
        I("PB-006", "presentation-audio", "advisory", "Name audio tiers"),
        I("PB-007", "validation-plan", "advisory", "Schedule a feel review"),
        I("PB-008", "scope", "advisory", "Confirm power-up list"));

    internal static CreativeReviewResult Attempt3 => Recorded("accepted", [],
        ["passed", "passed", "advisory", "advisory", "advisory", "passed"],
        I("PB-001", "scope", "advisory", "Confirm level count"),
        I("PB-002", "presentation-audio", "advisory", "Reference the neon identity"),
        I("PB-003", "controls-accessibility", "advisory", "Detail accessibility settings"),
        I("PB-004", "validation-plan", "advisory", "Schedule a feel review"),
        I("PB-005", "player-loop", "advisory", "Tune chain feedback"),
        I("PB-006", "presentation-audio", "advisory", "Name audio tiers"));

    [Fact]
    public void Attempt1_check_statuses_follow_its_issues()
    {
        var review = Normalize(Attempt1);
        Assert.Equal("changes-required", review.Disposition);
        Assert.Equal(new[] { "advisory", "passed", "advisory", "advisory", "blocking", "passed" }, Statuses(review));
        Assert.Equal("Add an explicit ownership and validation plan", Assert.Single(review.RequiredChanges));
        Assert.False(review.RequiresDirector);
    }

    [Fact]
    public void Attempt2_required_changes_are_the_blockers_exact_changes()
    {
        var review = Normalize(Attempt2);
        Assert.Equal("changes-required", review.Disposition);
        Assert.Equal(new[] { Attempt2.Issues[0].Change, Attempt2.Issues[1].Change }, review.RequiredChanges.ToArray());
        Assert.Equal("passed", review.Checklist.Single(x => x.Id == "constraints-non-goals").Status);
    }

    [Fact]
    public void Attempt3_is_accepted_with_its_advisory_observations()
    {
        var review = Normalize(Attempt3);
        Assert.Equal("accepted", review.Disposition);
        Assert.Empty(review.RequiredChanges);
        Assert.Equal(new[] { "advisory", "advisory", "advisory", "advisory", "advisory", "passed" }, Statuses(review));
        Assert.Equal(6, review.Issues.Count);
    }

    [Fact]
    public void Normalized_reviews_pass_the_rules_the_Director_applies()
    {
        foreach (var review in new[] { Normalize(Attempt1), Normalize(Attempt2), Normalize(Attempt3) })
        {
            Assert.True(VideoGameCreativeDirectorAgent.ValidReview(review));
            Assert.True(CreativeChecklist.MatchesDisposition(review.Disposition, review.RequiresDirector, review.RequiredChanges, review.Issues));
        }
    }

    [Fact]
    public void A_blocking_check_without_a_blocking_issue_is_sent_back_with_the_reason()
    {
        var review = Attempt3 with { Checklist = Attempt3.Checklist.Select(x => x.Id == "player-loop" ? x with { Status = "blocking" } : x).ToArray() };
        var issues = Attempt3.Issues.Where(x => x.CheckId != "player-loop").ToArray();
        var error = Assert.Throws<CreativeReviewContractException>(() => Normalize(review with { Issues = issues }));
        Assert.Contains("player-loop is blocking but has no open blocking issue", error.Message);
    }

    [Fact]
    public void An_escalation_needs_a_blocking_question()
    {
        var error = Assert.Throws<CreativeReviewContractException>(() => Normalize(Attempt3 with { Disposition = "escalate", RequiresDirector = true }));
        Assert.Contains("requiresDirector or escalate", error.Message);
    }

    [Fact]
    public void Escalation_with_a_blocking_question_is_kept()
    {
        var review = Normalize(Attempt1 with { Disposition = "escalate" });
        Assert.Equal("escalate", review.Disposition);
        Assert.True(review.RequiresDirector);
    }

    [Fact]
    public void An_earlier_blocker_cannot_be_dropped_or_downgraded()
    {
        var previous = Normalize(Attempt1).Issues;
        var dropped = Attempt3; // Its issues do not include pb-validation-owners-v1.
        Assert.Contains("pb-validation-owners-v1 is missing", Assert.Throws<CreativeReviewContractException>(() => Normalize(dropped, previous)).Message);
        var downgraded = Attempt1 with { Issues = Attempt1.Issues.Select(x => x.Severity == "blocking" ? x with { Severity = "advisory" } : x).ToArray() };
        Assert.Contains("must keep checkId", Assert.Throws<CreativeReviewContractException>(() => Normalize(downgraded, previous)).Message);
        var resolved = Attempt1 with { Issues = Attempt1.Issues.Select(x => x.Severity == "blocking" ? x with { Status = "resolved" } : x).ToArray() };
        Assert.Equal("accepted", Normalize(resolved, previous).Disposition);
    }

    [Fact]
    public void Missing_checks_and_incomplete_issues_are_named()
    {
        var review = Attempt1 with
        {
            Checklist = Attempt1.Checklist.Where(x => x.Id != "scope").ToArray(),
            Issues = [.. Attempt1.Issues, I("pb-empty", "scope", "advisory", " ") with { Owner = "" }]
        };
        var message = Assert.Throws<CreativeReviewContractException>(() => Normalize(review)).Message;
        Assert.Contains("checklist is missing scope", message);
        Assert.Contains("issue pb-empty is missing owner, change", message);
    }

    [Fact]
    public void Schema_offers_exactly_the_checklist_ids()
    {
        var ids = CreativeReviewModel.Schema.GetProperty("properties").GetProperty("checklist").GetProperty("items")
            .GetProperty("properties").GetProperty("id").GetProperty("enum").EnumerateArray().Select(x => x.GetString()!).ToArray();
        Assert.Equal(CreativeChecklist.CheckIds, ids);
    }

    [Fact]
    public async Task A_contradiction_is_corrected_in_the_same_turn_with_the_exact_reason()
    {
        var j = CreativeReviewTests.Fixture(); await j.Configure();
        var broken = Attempt3 with { Checklist = Attempt3.Checklist.Where(x => x.Id != "scope").ToArray() };
        j.ModelTexts.Enqueue(JsonSerializer.Serialize(broken, Web));
        j.ModelTexts.Enqueue("```json\n" + JsonSerializer.Serialize(Attempt3, Web) + "\n```");
        var result = await j.Agent.HandleCoordinationTurnAsync(j.ReviewRequest(), j.Context, default);
        Assert.Equal("Continue", result.Disposition);
        Assert.Equal("accepted", result.Artifact!.Payload.GetProperty("review").GetProperty("disposition").GetString());
        Assert.Equal(2, j.Models);
        Assert.Contains("checklist is missing scope", j.LastPrompt);
        Assert.Contains("Correction 1/2", j.LastPrompt);
        Assert.Equal(CreativeReviewModel.SchemaName, j.SchemaName);
    }

    [Fact]
    public async Task Exhausted_corrections_keep_the_text_the_Director_retries_on()
    {
        var j = CreativeReviewTests.Fixture(); await j.Configure();
        j.ModelText = "I could not produce a review.";
        var result = await j.Agent.HandleCoordinationTurnAsync(j.ReviewRequest(), j.Context, default);
        Assert.Equal("Blocked", result.Disposition);
        Assert.Equal("Creative review did not return actionable, consistent findings.", result.Content);
        Assert.Equal(1 + CreativeReviewModel.CorrectionLimit, j.Models);
        Assert.Contains("no JSON object", j.LastPrompt);
    }
}
