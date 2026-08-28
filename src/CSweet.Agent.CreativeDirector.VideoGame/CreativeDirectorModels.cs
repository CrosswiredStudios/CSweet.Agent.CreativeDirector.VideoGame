using CSweet.Agent.SDK;

namespace CSweet.Agent.CreativeDirector.VideoGame;

public enum CreativeDirectorPhase
{
    Discovery = 0,
    InvolvementConfirmation = 1,
    HighLevelGddWork = 2,
    HighLevelReview = 3,
    HighLevelAccepted = 4,
    PMPlanPending = 5,
    PMHiringPending = 6,
    DetailedDesign = 7,
    PackageReview = 8,
    DevelopmentReady = 9,
    Oversight = 10
}

public enum ManagerInvolvementMode
{
    Unspecified,
    Delegated,
    MilestoneReview,
    Collaborative
}

public sealed record ManagerPreferenceProfile
{
    public ManagerInvolvementMode InvolvementMode { get; init; } = ManagerInvolvementMode.Unspecified;
    public bool InvolvementWasExplicit { get; init; }
    public int InvolvementEvidenceCount { get; init; }
    public IReadOnlyList<string> PlatformConstraints { get; init; } = [];
    public IReadOnlyList<string> GenreConstraints { get; init; } = [];
    public IReadOnlyList<string> NarrativeConstraints { get; init; } = [];
    public string? StoryParticipation { get; init; }
    public string? ApprovalPreference { get; init; }
    public IReadOnlyList<string> ReferenceGuidance { get; init; } = [];
    public IReadOnlyList<Guid> SupportingMessageIds { get; init; } = [];
    public DateTimeOffset? UpdatedAt { get; init; }
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
    public Guid? OnboardingEventId { get; init; }
    public DateTimeOffset? OnboardingCompletedAt { get; init; }
    public ManagerPreferenceProfile ManagerPreferences { get; init; } = new();
    public IReadOnlyList<string> DiscoveryInputs { get; init; } = [];
    public IReadOnlyList<GamePitchRevision> Proposals { get; init; } = [];
    public AcceptedGameVision? AcceptedVision { get; init; }
    public Guid? VisionTodoId { get; init; }
    public Guid? HighLevelArtifactId { get; init; }
    public Guid? HighLevelLatestRevisionId { get; init; }
    public Guid? HighLevelAcceptedRevisionId { get; init; }
    public Guid? DetailedDesignPackageId { get; init; }
    public Guid? NarrativeWorldArtifactId { get; init; }
    public Guid? ArtAudioDirectionArtifactId { get; init; }
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
    IReadOnlyList<string> OpenDecisions)
{
    public Guid? HighLevelGddArtifactId { get; init; }
    public Guid? HighLevelGddAcceptedRevisionId { get; init; }
}

public sealed record GameVisionAcknowledgement(
    string AcceptedPitchDigest,
    bool Acknowledged,
    IReadOnlyList<string> Blockers,
    DateTimeOffset AcknowledgedAt);
