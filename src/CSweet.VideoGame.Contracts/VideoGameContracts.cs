using System.Text.Json;

namespace CSweet.VideoGame.Contracts;

public sealed record VideoGameProjectMetadataV1(
    string WorkingTitle,
    string Genre,
    IReadOnlyList<string> TargetPlatforms,
    string PlayerAudience,
    string EnginePreference,
    string ContentRatingTarget,
    IReadOnlyList<string> CreativePillars,
    string BusinessModel,
    bool LiveOperationsExpected,
    bool OnlineMultiplayerExpected,
    IReadOnlyList<string> AccessibilityTargets,
    IReadOnlyList<string> LocalizationTargets);

public static class VideoGameProfileKeys
{
    public const string ProductionV2 = "video-game-production.v2";
    public const string ProductionBoardV2 = "video-game-production-board.v2";
}

public static class VideoGameLifecyclePhases
{
    public const string Intake = "intake";
    public const string Concept = "concept";
    public const string PreProduction = "pre-production";
    public const string Prototype = "prototype";
    public const string VerticalSlice = "vertical-slice";
    public const string Production = "production";
    public const string Alpha = "alpha";
    public const string Beta = "beta";
    public const string ReleaseCandidate = "release-candidate";
    public const string Launch = "launch";
    public const string PostLaunchStabilization = "post-launch-stabilization";
    public const string LiveOperations = "live-operations";
    public const string Closure = "closure";
}

public static class VideoGameMilestoneKeys
{
    public const string VisionApproved = "vision-approved";
    public const string PreProductionReady = "pre-production-ready";
    public const string PrototypeValidated = "prototype-validated";
    public const string VerticalSliceApproved = "vertical-slice-approved";
    public const string AlphaExit = "alpha-exit";
    public const string BetaExit = "beta-exit";
    public const string ReleaseCandidateApproved = "release-candidate-approved";
    public const string LaunchApproved = "launch-approved";
    public const string StabilizationExit = "stabilization-exit";
    public const string SunsetApproved = "sunset-approved";
}

public static class VideoGameRoleKeys
{
    public const string CreativeDirector = "creative-director";
    public const string Producer = "game-producer";
    public const string GameDesigner = "game-designer";
    public const string TechnicalDirector = "game-technical-director";
    public const string Engineer = "game-engineer";
    public const string QualityAssurance = "game-quality-assurance";
    public const string PlaytestResearcher = "playtest-researcher";
    public const string ArtDirector = "game-art-director";
    public const string Artist = "game-artist";
    public const string TechnicalArtist = "technical-artist";
    public const string NarrativeDesigner = "narrative-designer";
    public const string AudioDesigner = "audio-designer";
    public const string LevelDesigner = "level-designer";
    public const string UserExperienceDesigner = "game-ui-ux-accessibility-designer";
    public const string BuildReleaseEngineer = "game-build-release-engineer";
    public const string NetworkingEngineer = "game-networking-engineer";
    public const string EconomyDesigner = "game-economy-designer";
    public const string LocalizationSpecialist = "game-localization-specialist";
    public const string SecurityPrivacy = "game-security-privacy";
    public const string MarketingCommunity = "game-marketing-community";
    public const string PlatformCertification = "game-platform-certification";
    public const string LiveOperations = "game-live-operations";
}

public static class VideoGameSpecializationKeys
{
    public const string Development = "video-game-development";
    public const string CreativeDirection = "game-creative-direction";
    public const string Production = "game-production";
    public const string Gameplay = "gameplay-systems";
    public const string Content = "game-content";
}

public static class VideoGameWorkItemTypeKeys
{
    public const string Milestone = "video-game.milestone.v1";
    public const string Feature = "video-game.feature.v1";
    public const string Content = "video-game.content.v1";
    public const string Task = "video-game.task.v1";
    public const string Bug = "video-game.bug.v1";
    public const string ResearchSpike = "video-game.research-spike.v1";
    public const string CreativeReview = "video-game.creative-review.v1";
}

public static class VideoGameArtifactTypeKeys
{
    public const string Vision = "video-game.vision.v1";
    public const string GameDesignDocument = "video-game.gdd.v1";
    public const string TechnicalDesign = "video-game.technical-design.v1";
    public const string ProductionPlan = "video-game.production-plan.v1";
    public const string NarrativeBible = "video-game.narrative-bible.v1";
    public const string ArtBible = "video-game.art-bible.v1";
    public const string AudioBible = "video-game.audio-bible.v1";
    public const string LevelContentPlan = "video-game.level-content-plan.v1";
    public const string UserExperienceAccessibility = "video-game.ux-accessibility.v1";
    public const string QualityEvaluationPlan = "video-game.qa-evaluation-plan.v1";
    public const string ReleasePlan = "video-game.release-plan.v1";
    public const string RunnableBuild = "video-game.runnable-build.v1";
}

public static class VideoGameRubricTypeKeys
{
    public const string Vision = "video-game.rubric.vision.v1";
    public const string GameDesign = "video-game.rubric.game-design.v1";
    public const string Creative = "video-game.rubric.creative-quality.v1";
    public const string Technical = "video-game.rubric.technical-feasibility.v1";
    public const string Quality = "video-game.rubric.quality.v1";
    public const string Accessibility = "video-game.rubric.accessibility.v1";
    public const string Performance = "video-game.rubric.performance.v1";
    public const string Release = "video-game.rubric.release.v1";
}

public static class VideoGameEvaluationTypeKeys
{
    public const string Playtest = "video-game.playtest.v1";
    public const string Accessibility = "video-game.accessibility-evaluation.v1";
    public const string Performance = "video-game.performance-evaluation.v1";
    public const string Certification = "video-game.platform-certification.v1";
}

public sealed record CreativeReviewRubric(
    string TypeKey,
    IReadOnlyList<CreativeReviewCriterion> Criteria,
    decimal PassingScore,
    bool BlockingFindingFailsReview);

public sealed record CreativeReviewCriterion(string Key, string Prompt, decimal Weight, bool Blocking);

public sealed record ToolchainRecommendation(
    string RecommendedAdapterKey,
    IReadOnlyList<ToolchainRecommendationOption> Options,
    string Rationale,
    IReadOnlyList<string> RequiredFeasibilityEvidenceTypeKeys,
    DateTimeOffset RecommendedAt);

public sealed record ToolchainRecommendationOption(
    string AdapterKey,
    IReadOnlyList<string> SupportedTargets,
    IReadOnlyList<string> Advantages,
    IReadOnlyList<string> Tradeoffs,
    IReadOnlyList<string> Risks,
    bool Eligible);

public sealed record PlaytestPlanV1(
    string ResearchQuestion,
    string ParticipantProfile,
    int TargetParticipantCount,
    IReadOnlyList<PlaytestTaskV1> Tasks,
    IReadOnlyList<PlaytestQuestionV1> Questions,
    IReadOnlyList<string> TelemetryKeys,
    string ConsentPolicyKey,
    string PrivacyNotes);

public sealed record PlaytestTaskV1(string Key, string Instruction, string SuccessSignal);
public sealed record PlaytestQuestionV1(string Key, string Prompt, string ResponseType, bool Required);
public sealed record PlaytestReportV1(
    int ParticipantCount,
    IReadOnlyList<PlaytestFindingV1> Findings,
    IReadOnlyDictionary<string, JsonElement> Metrics,
    string Recommendation,
    IReadOnlyList<string> FollowUpWorkItemKeys);
public sealed record PlaytestFindingV1(string Code, string Severity, bool Blocking, string Observation, string Interpretation);

public sealed record VideoGameStatusReportExtensionV1(
    string LifecyclePhase,
    string CurrentBuildStatus,
    string CreativeHealth,
    string TechnicalHealth,
    string QualityHealth,
    string PlayerValidationHealth,
    IReadOnlyList<string> CurrentMilestones,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<Guid> OpenDecisionIds,
    IReadOnlyList<Guid> EvidenceResourceIds);
