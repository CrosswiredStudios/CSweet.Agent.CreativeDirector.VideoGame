using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.Agent.SDK.WorkManagement;
using CSweet.WorkManagement.Contracts;
using Microsoft.Extensions.AI;

namespace CSweet.VideoGame.AgentKit;

public sealed record SpecialistWorkAssignment(
    AgentWorkContext Context,
    long AssignmentRevision,
    string RoleKey,
    string TaskTypeKey,
    string Instructions,
    IReadOnlyList<ExactArtifactInput> Documents,
    IReadOnlyList<EvidenceReference> Evidence,
    Guid ProducerOrganizationUserId,
    Guid CreativeDirectorOrganizationUserId);

public sealed record ExactArtifactInput(
    Guid ArtifactId, Guid RevisionId, string Sha256, string TypeKey, string Purpose);

public sealed record SpecialistDelivery(
    string Summary,
    Guid ArtifactId,
    Guid RevisionId,
    string Sha256,
    IReadOnlyList<EvidenceReference> Evidence,
    IReadOnlyList<string> RemainingRisks);

public sealed record SpecialistOperatingState
{
    public Guid WorkstreamId { get; init; }
    public Guid WorkItemId { get; init; }
    public Guid CorrelationId { get; init; }
    public long AssignmentRevision { get; init; }
    public string RoleKey { get; init; } = string.Empty;
    public string Status { get; init; } = "Assigned";
    public IReadOnlyDictionary<Guid, string> ExactInputDigests { get; init; } = new Dictionary<Guid, string>();
    public SpecialistDelivery? Delivery { get; init; }
    public string? Blocker { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public static class ProjectStateKeys
{
    public static string Portfolio(string roleKey) => $"video-game/{roleKey}/portfolio";
    public static string Workstream(string roleKey, Guid workstreamId) => $"video-game/{roleKey}/workstreams/{workstreamId:N}";
    public static string WorkItem(string roleKey, Guid workstreamId, Guid workItemId) =>
        $"video-game/{roleKey}/workstreams/{workstreamId:N}/items/{workItemId:N}";
}

public static class SpecialistAssignmentValidator
{
    public static void Validate(SpecialistWorkAssignment assignment, string expectedRoleKey)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        if (assignment.Context.OrganizationId == Guid.Empty || assignment.Context.WorkstreamId == Guid.Empty ||
            assignment.Context.BoardId is null || assignment.Context.WorkItemId is null || assignment.Context.CorrelationId == Guid.Empty)
            throw new ArgumentException("Specialist work requires broker-authenticated Workstream, board, work item, and correlation context.");
        if (!string.Equals(assignment.RoleKey, expectedRoleKey, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("The assignment is not owned by this specialist role.");
        if (assignment.AssignmentRevision < 1) throw new ArgumentException("Assignment revision is required.");
        if (assignment.Documents.Any(document => document.ArtifactId == Guid.Empty || document.RevisionId == Guid.Empty || !IsSha256(document.Sha256)))
            throw new ArgumentException("Every document input must bind an exact artifact revision and SHA-256 digest.");
    }

    private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
}

public static class SubstantiveOutputValidator
{
    private static readonly string[] PlaceholderMarkers =
        ["todo", "tbd", "lorem ipsum", "placeholder", "insert here", "coming soon", "to be decided"];

    public static void RequireSubstantiveMarkdown(string markdown, params string[] requiredSections)
    {
        if (string.IsNullOrWhiteSpace(markdown) || markdown.Length < 800)
            throw new InvalidOperationException("The durable deliverable is too short to be substantive.");
        var normalized = markdown.ToLowerInvariant();
        var marker = PlaceholderMarkers.FirstOrDefault(normalized.Contains);
        if (marker is not null) throw new InvalidOperationException($"The deliverable contains unresolved placeholder text: {marker}.");
        foreach (var section in requiredSections)
            if (!normalized.Contains(section.ToLowerInvariant(), StringComparison.Ordinal))
                throw new InvalidOperationException($"The deliverable is missing required section '{section}'.");
    }
}

public sealed class SpecialistBoardCoordinator(PlatformCapabilityClient platform)
{
    public async Task<WorkItem> StartAsync(SpecialistWorkAssignment assignment, string summary, CancellationToken token)
    {
        var boardId = assignment.Context.BoardId!.Value; var itemId = assignment.Context.WorkItemId!.Value;
        var item = await platform.Work.ReadItemAsync(new WorkItemReference(boardId, itemId), token);
        await platform.Work.CommentAsync(new CommentOnWorkItemRequest(boardId, itemId, summary,
            $"start-comment:{assignment.Context.CorrelationId:N}:{assignment.AssignmentRevision}")
            { Kind = "progress", CausationId = assignment.Context.CausationId?.ToString("D") }, token);
        if (item.Status.Equals("InProgress", StringComparison.OrdinalIgnoreCase) ||
            item.Status.Equals("In Progress", StringComparison.OrdinalIgnoreCase) ||
            item.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            return item;
        return await platform.Work.StartItemAsync(new TransitionWorkItemRequest(boardId, itemId, item.Revision,
            $"start:{assignment.Context.CorrelationId:N}:{assignment.AssignmentRevision}"), token);
    }

    public Task<WorkItemComment> BlockAsync(SpecialistWorkAssignment assignment, string blocker, CancellationToken token) =>
        platform.Work.CommentAsync(new CommentOnWorkItemRequest(assignment.Context.BoardId!.Value,
            assignment.Context.WorkItemId!.Value, blocker,
            $"block:{assignment.Context.CorrelationId:N}:{Digest(blocker)}")
        { Kind = "blocker", CausationId = assignment.Context.CausationId?.ToString("D") }, token);

    public async Task<WorkItem> CompleteAsync(SpecialistWorkAssignment assignment, SpecialistDelivery delivery, CancellationToken token)
    {
        if (delivery.ArtifactId == Guid.Empty || delivery.RevisionId == Guid.Empty || delivery.Evidence.Count == 0 ||
            delivery.Sha256.Length != 64 || delivery.Sha256.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidOperationException("Completion requires an exact durable artifact revision and attached evidence.");
        var boardId = assignment.Context.BoardId!.Value; var itemId = assignment.Context.WorkItemId!.Value;
        await platform.Work.CommentAsync(new CommentOnWorkItemRequest(boardId, itemId, delivery.Summary,
            $"delivery:{assignment.Context.CorrelationId:N}:{delivery.RevisionId:N}")
        { Kind = "evidence", ArtifactDigest = delivery.Sha256, CausationId = assignment.Context.CausationId?.ToString("D") }, token);
        var item = await platform.Work.ReadItemAsync(new WorkItemReference(boardId, itemId), token);
        return await platform.Work.CompleteItemAsync(new TransitionWorkItemRequest(boardId, itemId, item.Revision,
            $"complete:{assignment.Context.CorrelationId:N}:{delivery.RevisionId:N}"), token);
    }

    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16].ToLowerInvariant();
}

public sealed class RevisionSafeProjectState(PlatformCapabilityClient platform)
{
    public async Task<AgentOperatingState<T>> MergeAsync<T>(
        string stateKey, string schemaId, int schemaVersion, Func<T?, T> merge,
        IReadOnlyDictionary<string, string> sourceRevisions, string idempotencyKey, CancellationToken token)
        where T : class
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            AgentOperatingState<T>? current;
            try { current = await platform.ReadOperatingStateAsync<T>(stateKey, token); }
            catch (PlatformCapabilityException exception) when (exception.Code == PlatformCapabilityErrorCode.NotFound)
            { current = null; }
            var payload = merge(current?.Payload);
            try
            {
                var written = await platform.WriteOperatingStateAsync(new AgentOperatingStateWriteRequest(
                    stateKey, schemaId, schemaVersion, "Active", sourceRevisions, [], Fingerprint(payload), [], Guid.NewGuid(),
                    JsonSerializer.SerializeToElement(payload), current?.Revision, $"{idempotencyKey}:{attempt}"), token);
                return new AgentOperatingState<T>(written.Id, written.StateKey, written.SchemaId, written.SchemaVersion,
                    written.Status, written.SourceRevisions, written.ConditionCodes, written.DecisionFingerprint,
                    written.OpenCommitmentCorrelations, written.AttentionReviewId,
                    written.Payload.Deserialize<T>()!, written.Revision, written.CreatedAt, written.UpdatedAt);
            }
            catch (PlatformCapabilityException exception) when (exception.Code == PlatformCapabilityErrorCode.Conflict && attempt < 3) { }
        }
        throw new InvalidOperationException("Project state could not be merged after four revision conflicts.");
    }

    private static string Fingerprint<T>(T payload) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload))).ToLowerInvariant();
}

public abstract class VideoGameSpecialistAgentBase : CSweetAgentBase
{
    protected abstract string RoleKey { get; }
    public string DeclaredRoleKey => RoleKey;
    protected abstract string ArtifactTypeKey { get; }
    protected abstract string RolePrompt { get; }
    protected abstract IReadOnlyList<string> RequiredSections { get; }
    public abstract string PrimaryCapability { get; }

    protected override AgentConfigurationBuilder Configure(AgentConfigurationBuilder builder) => builder
        .LlmProvider("llmProviderId", "LLM provider", required: true,
            description: "Brokered model used to produce role-owned project deliverables.")
        .LlmModel("llmModel", "Model", "llmProviderId", required: true,
            description: "Model used for grounded specialist work.");

    protected override async Task<AgentWorkResult> ExecuteCapabilityCoreAsync(
        AgentCapabilityRequest request, AgentRuntimeContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Capability != PrimaryCapability)
            return AgentWorkResult.Failure($"Capability '{request.Capability}' is not supported.");
        SpecialistWorkAssignment? assignment;
        try { assignment = DeserializePayload<SpecialistWorkAssignment>(request.Arguments); }
        catch (JsonException) { return AgentWorkResult.Failure("The typed specialist assignment is invalid."); }
        if (assignment is null) return AgentWorkResult.Failure("The typed specialist assignment is required.");
        try { SpecialistAssignmentValidator.Validate(assignment, RoleKey); }
        catch (Exception exception) when (exception is ArgumentException or UnauthorizedAccessException)
        { return AgentWorkResult.Failure(exception.Message); }

        var stateKey = ProjectStateKeys.WorkItem(RoleKey, assignment.Context.WorkstreamId,
            assignment.Context.WorkItemId!.Value);
        AgentOperatingState<SpecialistOperatingState>? prior;
        try { prior = await context.Platform.ReadOperatingStateAsync<SpecialistOperatingState>(stateKey, cancellationToken); }
        catch (PlatformCapabilityException exception) when (exception.Code == PlatformCapabilityErrorCode.NotFound)
        { prior = null; }
        if (prior?.Payload is { Status: "Completed", Delivery: not null } completed &&
            completed.CorrelationId == assignment.Context.CorrelationId &&
            completed.AssignmentRevision == assignment.AssignmentRevision)
            return AgentWorkResult.Success(completed.Delivery);

        var stateStore = new RevisionSafeProjectState(context.Platform);
        _ = await stateStore.MergeAsync<SpecialistOperatingState>(stateKey,
            "com.csweet.video-game.specialist-work-state.v1", 1,
            current => new SpecialistOperatingState
            {
                WorkstreamId = assignment.Context.WorkstreamId,
                WorkItemId = assignment.Context.WorkItemId.Value,
                CorrelationId = assignment.Context.CorrelationId,
                AssignmentRevision = assignment.AssignmentRevision,
                RoleKey = RoleKey,
                Status = current?.Status == "Completed" ? current.Status : "InProgress",
                ExactInputDigests = assignment.Documents.ToDictionary(x => x.RevisionId, x => x.Sha256),
                Delivery = current?.Delivery,
                Blocker = null,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            assignment.Documents.ToDictionary(x => x.RevisionId.ToString("D"), x => x.Sha256),
            $"specialist-start:{assignment.Context.CorrelationId:N}:{assignment.AssignmentRevision}", cancellationToken);

        var board = new SpecialistBoardCoordinator(context.Platform);
        await board.StartAsync(assignment, $"{RoleKey} accepted typed assignment {assignment.Context.CorrelationId:D}.", cancellationToken);
        try
        {
            var grounding = new List<object>();
            foreach (var input in assignment.Documents)
            {
                var document = await context.Platform.Artifacts.GetAsync(input.ArtifactId, cancellationToken);
                var inputRevision = document.Revisions.SingleOrDefault(x => x.Id == input.RevisionId)
                    ?? throw new InvalidOperationException($"Exact artifact revision {input.RevisionId:D} is unavailable.");
                if (!string.Equals(inputRevision.ContentSha256, input.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Artifact revision {input.RevisionId:D} no longer matches its assigned hash.");
                grounding.Add(new { input.TypeKey, input.Purpose, input.ArtifactId, input.RevisionId, input.Sha256, inputRevision.Content });
            }
            var provider = Settings.GetGuid("llmProviderId")
                ?? throw new InvalidOperationException("A brokered LLM provider must be configured.");
            var client = context.CreateChatClient(new AgentLlmSelection(provider, Settings.GetString("llmModel"),
                new AgentLlmInvocationContext(null, null, $"video-game-specialist:{RoleKey}")));
            var response = await client.GetResponseAsync([
                new ChatMessage(ChatRole.System, $"{RolePrompt}\nYou own only {RoleKey} accountability. Produce concrete, testable Markdown with explicit decisions, dependencies, acceptance evidence, and no placeholders. Do not absorb another specialist's accountability."),
                new ChatMessage(ChatRole.User, $"Authoritative board assignment:\n{assignment.Instructions}\n\nExact authorized inputs:\n{JsonSerializer.Serialize(grounding)}\n\nExisting evidence:\n{JsonSerializer.Serialize(assignment.Evidence)}")
            ], cancellationToken: cancellationToken);
            var markdown = response.Text ?? string.Empty;
            SubstantiveOutputValidator.RequireSubstantiveMarkdown(markdown, RequiredSections.ToArray());
            var artifact = await context.Platform.Artifacts.CreateAsync(new CreateArtifactDocument(
                $"{RoleKey}: {assignment.TaskTypeKey}", markdown, ArtifactTypeKey,
                $"artifact:{assignment.Context.CorrelationId:N}:{assignment.AssignmentRevision}",
                OriginWorkItemId: assignment.Context.WorkItemId)
            { WorkstreamId = assignment.Context.WorkstreamId, TeamId = assignment.Context.TeamId }, cancellationToken);
            var revision = artifact.Revisions.Single(x => x.Id == artifact.LatestRevisionId);
            await context.Platform.Artifacts.SubmitAsync(new SubmitArtifactRevision(artifact.Id, revision.Id,
                $"submit:{assignment.Context.CorrelationId:N}:{revision.Id:N}", ReviewerOrganizationUserId: assignment.ProducerOrganizationUserId), cancellationToken);
            var evidence = new EvidenceReference("ArtifactRevision", artifact.Id, revision.Id, revision.ContentSha256,
                ArtifactTypeKey, "Submitted");
            var delivery = new SpecialistDelivery(
                $"Submitted exact {ArtifactTypeKey} revision {revision.Id:D} ({revision.ContentSha256}).",
                artifact.Id, revision.Id, revision.ContentSha256, [evidence], []);
            await board.CompleteAsync(assignment, delivery, cancellationToken);
            _ = await stateStore.MergeAsync<SpecialistOperatingState>(stateKey,
                "com.csweet.video-game.specialist-work-state.v1", 1,
                current => (current ?? new SpecialistOperatingState()) with
                {
                    WorkstreamId = assignment.Context.WorkstreamId,
                    WorkItemId = assignment.Context.WorkItemId.Value,
                    CorrelationId = assignment.Context.CorrelationId,
                    AssignmentRevision = assignment.AssignmentRevision,
                    RoleKey = RoleKey,
                    Status = "Completed",
                    ExactInputDigests = assignment.Documents.ToDictionary(x => x.RevisionId, x => x.Sha256),
                    Delivery = delivery,
                    Blocker = null,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                assignment.Documents.ToDictionary(x => x.RevisionId.ToString("D"), x => x.Sha256),
                $"specialist-complete:{assignment.Context.CorrelationId:N}:{revision.Id:N}", cancellationToken);
            await context.Platform.Communication.SendDirectMessageAsync(assignment.ProducerOrganizationUserId,
                $"{RoleKey} completed {assignment.Context.WorkItemId:D}. Review artifact {artifact.Id:D}, revision {revision.Id:D}, SHA-256 {revision.ContentSha256}.",
                $"producer-report:{assignment.Context.CorrelationId:N}:{revision.Id:N}", assignment.Context, cancellationToken);
            return AgentWorkResult.Success(delivery);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await board.BlockAsync(assignment, $"{RoleKey} blocked: {exception.Message}", cancellationToken);
            _ = await stateStore.MergeAsync<SpecialistOperatingState>(stateKey,
                "com.csweet.video-game.specialist-work-state.v1", 1,
                current => (current ?? new SpecialistOperatingState()) with
                {
                    WorkstreamId = assignment.Context.WorkstreamId,
                    WorkItemId = assignment.Context.WorkItemId.Value,
                    CorrelationId = assignment.Context.CorrelationId,
                    AssignmentRevision = assignment.AssignmentRevision,
                    RoleKey = RoleKey,
                    Status = "Blocked",
                    ExactInputDigests = assignment.Documents.ToDictionary(x => x.RevisionId, x => x.Sha256),
                    Blocker = exception.Message,
                    UpdatedAt = DateTimeOffset.UtcNow
                },
                assignment.Documents.ToDictionary(x => x.RevisionId.ToString("D"), x => x.Sha256),
                $"specialist-blocked:{assignment.Context.CorrelationId:N}:{Digest(exception.Message)}", cancellationToken);
            await context.Platform.Communication.SendDirectMessageAsync(assignment.ProducerOrganizationUserId,
                $"{RoleKey} is blocked on work item {assignment.Context.WorkItemId:D}: {exception.Message}",
                $"producer-blocker:{assignment.Context.CorrelationId:N}:{Digest(exception.Message)}", assignment.Context, cancellationToken);
            return AgentWorkResult.Failure(exception.Message);
        }
    }

    private static string Digest(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16].ToLowerInvariant();
}

public static class VideoGameSpecialistConformance
{
    private static readonly string[] RequiredCapabilities =
    [
        "work.item.read", "work.item.comment", "work.item.start", "work.item.complete",
        "platform.artifact.read.v1", "platform.artifact.create.v1", "platform.artifact.submit.v1",
        "communication.chat.create.v1", "communication.message.send.v1",
        "platform.agent-operating-state.read.v1", "platform.agent-operating-state.write.v1"
    ];

    public static IReadOnlyList<string> ValidateManifest(
        string manifestPath,
        string expectedAgentId,
        string expectedRoleKey,
        string expectedCapability)
    {
        var errors = new List<string>();
        using var document = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        var root = document.RootElement;
        if (!root.TryGetProperty("kind", out var kind) || kind.GetString() != "agent")
            errors.Add("Specialist packages must declare kind=agent.");
        if (!root.TryGetProperty("id", out var id) || id.GetString() != expectedAgentId)
            errors.Add("The package id does not match the specialist implementation.");
        var roles = root.GetProperty("rolePolicy").GetProperty("declaredRoleKeys")
            .EnumerateArray().Select(x => x.GetString()).Where(x => x is not null).ToHashSet(StringComparer.Ordinal);
        if (!roles.SetEquals([expectedRoleKey]))
            errors.Add("Every required specialist package must declare exactly its one accountable role.");
        var provided = root.GetProperty("provides").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString()).Where(x => x is not null).ToHashSet(StringComparer.Ordinal);
        if (!provided.Contains(expectedCapability))
            errors.Add("The role execution capability is not declared.");
        var required = root.GetProperty("requires").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString()).Where(x => x is not null).ToHashSet(StringComparer.Ordinal);
        foreach (var capability in RequiredCapabilities)
            if (!required.Contains(capability)) errors.Add($"Required conformance capability '{capability}' is missing.");
        if (required.Any(x => x is "platform.publication.execute.v1" or "platform.publication.publish.v1"))
            errors.Add("Specialists cannot acquire direct public-publication authority.");
        var runtime = root.GetProperty("runtime");
        if (!runtime.GetProperty("supportsMultipleInstallations").GetBoolean())
            errors.Add("Specialist packages must support distinct project-scoped installations.");
        return errors;
    }

    public static bool StateKeysAreIsolated(string roleKey, Guid firstWorkstream, Guid firstItem,
        Guid secondWorkstream, Guid secondItem)
    {
        var values = new[]
        {
            ProjectStateKeys.Workstream(roleKey, firstWorkstream),
            ProjectStateKeys.WorkItem(roleKey, firstWorkstream, firstItem),
            ProjectStateKeys.Workstream(roleKey, secondWorkstream),
            ProjectStateKeys.WorkItem(roleKey, secondWorkstream, secondItem)
        };
        return values.Distinct(StringComparer.Ordinal).Count() == values.Length;
    }
}
