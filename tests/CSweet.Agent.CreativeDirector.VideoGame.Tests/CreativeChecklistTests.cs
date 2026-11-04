using System.Text.Json;
using CSweet.Agent.SDK;
using CrosswiredStudios.VideoGame.ProductionReview;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class CreativeChecklistTests
{
    private static CreativeReviewResult Review(params CreativeIssue[] issues) => new(
        issues.Any(x => x.Severity == "blocking" && x.Status == "open") ? "changes-required" : "accepted",
        "Reviewed the exact creative requirements", issues.Select(x => x.Evidence).ToArray(),
        CreativeChecklist.Blockers(issues).Select(x => x.Change).ToArray(), false)
    {
        ChecklistVersion = CreativeChecklist.Version, Issues = issues,
        Checklist = CreativeChecklist.CheckIds.Select(id => new CreativeCheck(id,
            issues.Any(x => x.CheckId == id && x.Status == "open" && x.Severity == "blocking") ? "blocking" :
            issues.Any(x => x.CheckId == id && x.Status == "open") ? "advisory" : "passed", "Checked the approved source and current section")).ToArray()
    };
    private static CreativeIssue Issue => new("keyboard", "controls-accessibility", "blocking", "open", "Keyboard alternative",
        "Controls section omits keyboard input", "game-producer", "pre-production", "Record the keyboard requirement");

    [Fact]
    public async Task AdvisoryOnlyReviewAcceptsWithoutPromotingSuggestionsToRequiredChanges()
    {
        var j = CreativeReviewTests.Fixture(); await j.Configure();
        j.ModelText = JsonSerializer.Serialize(Review(Issue with { Severity = "advisory", Requirement = "Optional cue recommendation" }));
        var result = await j.Agent.HandleCoordinationTurnAsync(j.ReviewRequest(), j.Context, default);
        Assert.Equal("Continue", result.Disposition);
        Assert.Equal("accepted", result.Artifact!.Payload.GetProperty("review").GetProperty("disposition").GetString());
        Assert.Empty(result.Artifact.Payload.GetProperty("review").GetProperty("requiredChanges").EnumerateArray());
    }

    [Theory]
    [InlineData("resolved", true)]
    [InlineData("dropped", false)]
    [InlineData("downgraded", false)]
    [InlineData("renamed-requirement", false)]
    public async Task RevisionsMustAccountForPriorBlockers(string change, bool valid)
    {
        var j = CreativeReviewTests.Fixture(); await j.Configure();
        j.ModelText = JsonSerializer.Serialize(Review(Issue));
        var firstRequest = j.ReviewRequest();
        Assert.Equal("Continue", (await j.Agent.HandleCoordinationTurnAsync(firstRequest, j.Context, default)).Disposition);
        var doc = j.ReviewDocument!;
        var next = doc.Revisions[0] with { Id = Guid.NewGuid(), Number = 2, ContentSha256 = new string('d',64), Content = "Required keyboard path is now explicit" };
        j.ReviewDocument = doc with { LatestRevisionId = next.Id, Revisions = [.. doc.Revisions, next] };
        var issue = Issue with { Status = "resolved", Evidence = "Controls now explicitly require keyboard movement" };
        if (change == "downgraded") issue = issue with { Severity = "advisory" };
        if (change == "renamed-requirement") issue = issue with { Requirement = "A different optional rule" };
        j.ModelText = JsonSerializer.Serialize(change == "dropped" ? Review() : Review(issue));
        var result = await j.Agent.HandleCoordinationTurnAsync(j.ReviewRequest(), j.Context, default);
        Assert.Contains("previousReview", j.UserPrompt);
        Assert.Contains("keyboard", j.UserPrompt);
        Assert.Equal(valid ? "Continue" : "Blocked", result.Disposition);
        if (valid)
        {
            // Replaying the older completed review must not roll the finding ledger back.
            await j.Agent.HandleCoordinationTurnAsync(firstRequest, j.Context, default);
            Assert.Equal(next.ContentSha256, j.ReviewLedger.SourceRevisions["evidence"]);
            Assert.Contains("resolved", j.ReviewLedger.Payload.GetRawText());
            var third = next with { Id = Guid.NewGuid(), Number = 3, ContentSha256 = new string('e',64) };
            j.ReviewDocument = j.ReviewDocument with { LatestRevisionId = third.Id, Revisions = [.. j.ReviewDocument.Revisions, third] };
            await j.Agent.HandleCoordinationTurnAsync(j.ReviewRequest(), j.Context, default);
            Assert.Contains("resolved", j.UserPrompt);
        }
    }

    [Fact]
    public async Task NewModelOutputNeedsEveryChecklistItemAndConsistentBlockingChanges()
    {
        foreach (var review in new[] {
            new CreativeReviewResult("accepted", "No checklist", [], [], false),
            Review() with { Checklist = [] },
            Review(Issue) with { RequiredChanges = [] },
            Review(Issue) with { Disposition = "accepted" },
            Review(Issue with { Severity = "advisory" }) with { Disposition = "escalate", RequiresDirector = true }
        })
        {
            var j = CreativeReviewTests.Fixture(); await j.Configure(); j.ModelText = JsonSerializer.Serialize(review);
            Assert.Equal("Blocked", (await j.Agent.HandleCoordinationTurnAsync(j.ReviewRequest(), j.Context, default)).Disposition);
        }
    }
}
