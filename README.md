# C-Sweet Video Game Creative Director

Provide durable video game vision and creative leadership from discovery through pitch approval, dedicated-studio formation, certified-toolchain selection, and production oversight.

## Contract

- Package ID: `com.csweet.video-game-creative-director`
- Version: `1.6.3`
- Provides: `creative-direction.game-vision.v1`
- Role profile: `manager.v1`
- Declared role: `creative-director`
- Catalog role: `video-game-creative-director`
- Specializations: `video-game-development`, `game-creative-direction`
- Activation: always on, with five-minute attention reviews
- Protocol: v2
- Network access: none

The agent has no credentials and requests no unrestricted filesystem, hiring, spending, marketplace,
or web authority. It proposes a governed Producer bootstrap staffing plan, but it cannot approve,
source, install, spend, or claim that hiring is complete.

## Lifecycle

`Discovery → InvolvementConfirmation → HighLevelReview/HighLevelAccepted → TeamPlanPending → TeamStaffingPending → WorkstreamPlanPending → ProjectSetup → DetailedDesign → PackageReview → Oversight`

Onboarding invites starting context and references without embedding a plain-text question. On the
manager's first turn, the agent uses the platform's structured multiple-choice tool to choose
delegated decisions, milestone review, or close collaboration. Manager decisions use 2–4 concrete
options with one recommendation instead of open-ended questions whenever an active chat turn is
available. A mode-only answer is persisted and acknowledged without invoking the model or document
toolchain; pitch production begins after the manager supplies creative direction, references, or asks
the Creative Director to originate concepts. The agent does not submit staffing until that choice is recorded. PNG, JPEG, WebP, PDF, UTF-8 text, and Markdown
references are passed to the configured model through opaque, broker-validated media references;
operating state and memory retain metadata and digests only, never raw files or private paths.

Delegated mode lets the Creative Director lock the initial vision and start the governed staffing plan on the
manager's first substantive turn. In review modes, each pitch revision produces exactly one concise
Creative Director message with the exact submitted document attached. Review and change requests
live in the document workspace; the attachment's More menu may approve that same exact revision as
a convenience shortcut, but the agent does not create a duplicate multiple-choice card. The agent
remains in `HighLevelReview` and emits no autonomous follow-up chat until the document revision is
decided. The resulting exact-revision event wakes the agent, verifies the artifact and revision,
and creates one visible personal staffing-plan card before confirming the next step. That claimed
card owns submission of the governed proposal, so interruption or capability failure remains visible
and retryable. Requesting changes keeps the premise in review and captures bounded feedback
before another revision is created. While pitch creation
or revision is running, the active turn immediately shows a short conversational acknowledgement;
the final review response replaces that provisional text instead of adding another permanent message.
Collaborative mode supports iterative refinement until the manager locks an exact revision. The
authoritative operating state stores the involvement mode, platform/genre constraints, story and
approval preferences, reference guidance, supporting message IDs, and update time.

Every formal pitch is revisioned and digest-bound. Only the authoritative manager can accept the
latest exact digest except in explicitly delegated mode, where the Creative Director locks the
initial revision. The initial plan contains only the Producer as operational lead, with the Creative Director supervising outside ordinary team
membership. The Producer proposes further roles from scoped work and capability gaps. As soon as the Producer is active, the agent hands over a typed
`creative-direction.game-vision-brief.v1` artifact,
and enters oversight only after an exact-digest, blocker-free
`product-management.game-vision-acknowledgement.v1` response.

During oversight it answers creative questions, escalates decisions owned by other roles, relays the
manager's answer to the original worker, consumes subordinate status reports, and emits attributed
management reporting without unchanged-state chatter.

Inbound chat is classified before lifecycle-specific work. Status and bounded information questions
are answered without creating phantom work; acknowledgements remain task-free; authenticated
project-scoped creative action requests create one correlated personal agenda card. The SDK claims
Ready cards, and the agent completes, defers, or blocks each bounded unit explicitly. Human
multiple-choice answers return as durable chat turns, while agent-to-agent work uses direct messages
for simple exchanges and typed coordination for multi-turn or evidence-bound collaboration.
Attention review also ensures one stable portfolio-review card per indexed game. Active development
is revisited on a four-hour safety-net cadence and ongoing oversight daily; project events and chat
can wake bounded work sooner. Oversight explicitly includes launch, stabilization, live operations,
updates, expansions, DLC, and sequel recommendation. A sequel that is approved becomes a separate
project rather than being folded into the predecessor's state.

## Develop

```powershell
dotnet test
dotnet run --project src/CSweet.Agent.CreativeDirector.VideoGame -- --self-test
```

The tests run entirely in memory and require no C-Sweet instance or credentials.

## Install

Keep `csweet-plugin.json` at the repository root. Import a reviewed GitHub commit in C-Sweet, or
clone this repository as an immediate child of C-Sweet's configured local agent catalog. Review
the exact manifest, grants, activation mode, and source before approving installation.

Creative work is grounded in authoritative business, finance, organization/team state, approved
user/business memory, and supplied broker references. Explicit preferences and project decisions
may be proposed to governed memory immediately; inferred persona preferences are not persisted
from a single observation and remain subject to platform approval.

Built with `CSweet.Agent.SDK` 3.31.1, `CSweet.Memory` 0.1.2.


## Extension ownership and isolated builds

Game-specific payload helpers and decision logic live in the bundled `extensions/video-game` source snapshot under the publisher-owned `CrosswiredStudios.VideoGame` namespace. They are compiled into this agent, not published as C-Sweet platform contracts. The snapshot has versioned SHA-256 provenance and needs no sibling checkout or domain NuGet feed. C-Sweet handles generic coordination envelopes and profile metadata; agent permissions and existing wire type IDs remain unchanged.

## Adaptive staffing (1.6.3 / production profile revision 4)

New projects bootstrap only the Producer. The accepted vision and project board are handed over while
technical leadership, assets, toolchains and delivery hires are still being arranged. The Producer owns
workload-backed team proposals; the Creative Director reviews them against actual planning/backlog needs.
Revision 4 requires only the Producer at project level and the technical lead for detailed decomposition.
QA readiness and independent release verification remain required; hiring approval never authorizes spending or launch.
Existing workstreams keep their pinned profile revision and are not silently migrated or downsized.

Roster reads respect the platform limit of 100 entries per page. Bootstrap handoff requires
the host roster fix allowing the active team lead's direct manager to inspect the team before
Workstream creation, and including provided work capabilities in roster eligibility.

## Pitch refinement before staffing

The Creative Director supplies exact accepted pitch and GDD references. The Producer reads their
contents and iterates a single shared production-brief document, asking focused questions while
the Creative Director contributes answers and revised wording. The accepted pitch remains the
scope authority; the working brief records delivery detail, assumptions and unresolved questions.
Each turn and document revision is durable. Model decisions are cached per session/turn before
document writes so retries retain the same decision. No staffing handoff is recorded until the
Producer reports justified confidence with zero planning questions and the Creative Director
accepts that exact revision. A stalled or bounded conversation never counts as readiness.
The accepted brief includes immutable exact pitch/GDD source appendices and is packaged for technical planning.
New coordination payloads are pitch-brief.v1, pitch-review.v1 and pitch-reply.v1 under
video-game.production. Existing completed staffing decisions are not silently revoked.
Reimport both agents to enable this protocol; legacy unrefined handoffs are blocked.

## Shared collaboration SDK

Uses CSweet.Agent.SDK 3.31.1 typed document references, explicit coordination read sharing,
accepted-revision lookup, and handoff readiness checks. Pitch content and staffing judgment
remain agent-specific. See the SDK's `docs/collaboration.md` for reusable documentation requests,
clarification, review, and personal-work dependency waits. The matching C-Sweet host is required
for sharing at every coordination start; package import alone does not update the host.

## Provider queue handling

Uses SDK 3.31.1 for acknowledged LLM waiting, conversation activity, and host-authoritative deadline updates. Deploy the matching C-Sweet AgentHost and reimport this package to enable the private polling protocol.

## Producer hiring kickoff

A direct message from an active Producer on an approved project team resumes that project's
setup and accepted-brief handoff, even before a Workstream exists. The Director resolves the
sender from authenticated context and checks the governed team roster; message text cannot
assign a role or approve the vision. The exact-document refinement workflow still controls
creative acceptance and the Producer's subsequent staffing proposals.
