using CSweet.Agent.SDK;

namespace CSweet.Agent.CreativeDirector.VideoGame;

public enum CreativeDirectorPhase
{
    Discovery,
    PitchReview,
    VisionAccepted,
    PMPlanPending,
    PMHiringPending,
    VisionHandoff,
    Oversight
}

public sealed record GamePitchRevision(
    int Revision,
    string Markdown,
    string Digest,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> PositiveConstraints,
    IReadOnlyList<string> ReferenceDigests);

public sealed record AcceptedGameVision(
    int Revision,
    string Digest,
    string Markdown,
    Guid ConversationId,
    Guid ChatTurnId,
    Guid MessageId,
    DateTimeOffset AcceptedAt);

public sealed record ReferenceEvidence(
    Guid AttachmentId,
    Guid ConversationId,
    Guid MessageId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    string Observation);

public sealed record PendingCreativeEscalation(
    Guid RequestingEmployeeId,
    Guid ConversationId,
    Guid SourceMessageId,
    string Question,
    DateTimeOffset EscalatedAt,
    bool Relayed = false);

public sealed record CreativeDirectorOperatingState
{
    public CreativeDirectorPhase Phase { get; init; } = CreativeDirectorPhase.Discovery;
    public bool IntakeChoiceAsked { get; init; }
    public IReadOnlyList<string> DiscoveryInputs { get; init; } = [];
    public IReadOnlyList<GamePitchRevision> Proposals { get; init; } = [];
    public AcceptedGameVision? AcceptedVision { get; init; }
    public IReadOnlyList<ReferenceEvidence> References { get; init; } = [];
    public Guid? StaffingRequestId { get; init; }
    public Guid? ProductManagerEmployeeId { get; init; }
    public Guid? HandoffSessionId { get; init; }
    public IReadOnlyList<PendingCreativeEscalation> PendingEscalations { get; init; } = [];
    public IReadOnlyList<ManagementStatusReport> SubordinateReports { get; init; } = [];
    public IReadOnlyList<string> NotificationFingerprints { get; init; } = [];
    public DateOnly? LastDailyReportDate { get; init; }
}

public sealed record GameVisionBrief(
    string AcceptedPitchDigest,
    string PlayerAndProductOutcome,
    string GameplayLoopAndCreativePillars,
    string PlatformAndStackConstraints,
    string ArtNarrativeAudioAndToneDirection,
    string MvpScopeAndNonGoals,
    IReadOnlyList<ReferenceEvidence> ReferenceSummaries,
    string SuccessCriteriaRisksAndAssumptions,
    IReadOnlyList<string> OpenDecisions);

public sealed record GameVisionAcknowledgement(
    string AcceptedPitchDigest,
    bool Acknowledged,
    IReadOnlyList<string> Blockers,
    DateTimeOffset AcknowledgedAt);
