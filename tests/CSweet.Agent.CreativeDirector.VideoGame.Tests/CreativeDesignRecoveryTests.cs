using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.Agent.SDK.VideoGame.Contracts;

namespace CSweet.Agent.CreativeDirector.VideoGame.Tests;

public sealed class CreativeDesignRecoveryTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static string IncidentReply => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "shatterchord-gdd-2026-11-12.excerpt.md"));

    [Fact]
    public async Task Incident_heading_format_is_submitted_unchanged_for_independent_semantic_review()
    {
        var j = new CreativeScopeTests.Journey(); await j.Configure();
        j.ModelTexts.Enqueue(IncidentReply);
        var request = j.Request();
        var result = await j.Agent.HandleCoordinationTurnAsync(request, j.Context, default);
        Assert.Equal("Continue", result.Disposition);
        Assert.Equal(1, j.Models);
        Assert.Contains("No exact heading names, levels or order are required", j.SystemPrompt);
        Assert.Single(j.Documents);
        Assert.Equal(IncidentReply.Trim(), j.Documents.Values.Single().Revisions[0].Content);
        var replay = await j.Agent.HandleCoordinationTurnAsync(request, j.Context, default);
        Assert.Equal(result.Artifact!.Payload.GetRawText(), replay.Artifact!.Payload.GetRawText());
        Assert.Equal(1, j.Models);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revision_recovery_preserves_exact_base_and_findings_and_never_submits_invalid_output(bool correctionFails)
    {
        var j = new CreativeScopeTests.Journey(); await j.Configure();
        var initial = j.Request();
        var submitted = await j.Agent.HandleCoordinationTurnAsync(initial, j.Context, default);
        var payload = submitted.Artifact!.Payload;
        var documentId = payload.GetProperty("documentId").GetGuid();
        var baseId = payload.GetProperty("revisionId").GetGuid();
        const string finding = "Keep the approved desktop-web scope and add a concrete chain rule.";
        var request = initial with { ExpectedRevision = 3, TurnOrdinal = 3, Transcript = [..initial.Transcript,
            new(Guid.NewGuid(),1,j.Creative,"Continue",submitted.Content,DateTimeOffset.UtcNow,
                new(submitted.Artifact.Type,"1.0",submitted.Artifact.Key,1,true,payload,"result")),
            new(Guid.NewGuid(),2,j.Manager,"Continue","Revise the GDD",DateTimeOffset.UtcNow,
                new("video-game.creative-design.feedback.v1","1.0",submitted.Artifact.Key,1,true,
                    JsonSerializer.SerializeToElement(new { documentId, revisionId = baseId, summary = finding },Json),"feedback"))] };
        j.ModelTexts.Enqueue("");
        if (correctionFails) j.ModelTexts.Enqueue(" ");
        var result = await j.Agent.HandleCoordinationTurnAsync(request, j.Context, default);
        Assert.Equal(correctionFails ? "Blocked" : "Continue", result.Disposition);
        Assert.Equal(3, j.Models);
        Assert.Contains(finding, j.UserPrompt);
        Assert.Contains("Prior GDD:\n" + CreativeScopeTests.Journey.Markdown, j.UserPrompt);
        Assert.Single(j.Documents);
        Assert.Equal(correctionFails ? 0 : 1, j.RevisionWrites);
        Assert.Equal(correctionFails ? 1 : 2, j.Submissions);
        if (correctionFails)
        {
            var failure = result.Artifact!.Payload.Deserialize<GameCreativeDesignFailureV1>(Json)!;
            Assert.Equal(GameCreativeDesignProtocolV1.FailureType,result.Artifact.Type);
            Assert.Equal(GameCreativeDesignFailureCodes.EmptyGeneration,failure.Code);
            Assert.Equal(j.Project,failure.WorkstreamId);
            Assert.Contains("Video Game Director owns recovery", result.Content);
            Assert.Contains("no document content", result.Content);
            Assert.Equal(baseId, j.Documents.Values.Single().LatestRevisionId);
            var replay = await j.Agent.HandleCoordinationTurnAsync(request, j.Context, default);
            Assert.Equal(result.Content, replay.Content);
            Assert.Equal(result.Artifact.Payload.GetRawText(), replay.Artifact!.Payload.GetRawText());
            Assert.Equal(3, j.Models); // Exhausted evidence is not a validated draft or a new generation.
            Assert.Equal(0, j.RevisionWrites);
            // A later authorized session resume can retry the same exact base, without rewriting evidence.
            var recovered = await j.Agent.HandleCoordinationTurnAsync(request with { ExpectedRevision = 5 }, j.Context, default);
            Assert.Equal("Continue", recovered.Disposition);
            Assert.Equal(4, j.Models);
        }
        else
        {
            var revised = j.Documents.Values.Single().Revisions.Single(x => x.Id != baseId);
            Assert.Equal(baseId, revised.BaseRevisionId);
            Assert.Equal(documentId, result.Artifact!.Payload.GetProperty("documentId").GetGuid());
            Assert.Equal(revised.Id, result.Artifact.Payload.GetProperty("revisionId").GetGuid());
        }
    }

    [Fact]
    public async Task Empty_response_is_corrected_once_and_output_limit_never_submits_a_partial_gdd()
    {
        var j = new CreativeScopeTests.Journey(); await j.Configure();
        j.ModelTexts.Enqueue("");
        Assert.Equal("Continue", (await j.Agent.HandleCoordinationTurnAsync(j.Request(), j.Context, default)).Disposition);
        Assert.Equal(2, j.Models);
        var limited = new CreativeScopeTests.Journey { FinishReason = "length" }; await limited.Configure();
        var blocked = await limited.Agent.HandleCoordinationTurnAsync(limited.Request(), limited.Context, default);
        Assert.Equal("Blocked", blocked.Disposition);
        Assert.Contains("output limit", blocked.Content);
        Assert.Empty(limited.Documents);
        Assert.Equal(2, limited.Models);
    }

    [Theory]
    [InlineData("Intent: satisfying resonance. Player: expressive control. Rules: break bricks. Look/sound: reactive neon chiptune. Evaluation: Director observes the loop. Limits: desktop web. Pending: tune the chain with the designer.")]
    [InlineData("| Topic | Plan |\n| Experience | Expressive Breakout |\n| Systems | Paddle, ball, chains |\n| Presentation | Reactive neon/audio |\n| Verification | Director observes keyboard play |\n| Guardrails | Desktop web |\n| Pending work | Designer tunes chains |")]
    [InlineData("### Rules and play\nPaddle and ball.\n# Intent and presentation\nExpressive neon music.\nQuestions and limits: web only, tuning remains.\nAcceptance is future Director observation.")]
    public async Task Different_document_structures_do_not_trigger_format_correction(string content)
    {
        var j = new CreativeScopeTests.Journey { ModelText=content }; await j.Configure();
        var result=await j.Agent.HandleCoordinationTurnAsync(j.Request(),j.Context,default);
        Assert.Equal("Continue",result.Disposition);
        Assert.Equal(1,j.Models);
        Assert.Equal(content,j.Documents.Values.Single().Revisions[0].Content);
    }
}
