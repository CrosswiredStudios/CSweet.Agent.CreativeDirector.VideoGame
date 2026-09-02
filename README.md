# C-Sweet Video Game Creative Director

Provide durable video game vision and creative leadership from discovery through pitch approval, dedicated-studio formation, certified-toolchain selection, and production oversight.

## Contract

- Package ID: `com.csweet.video-game-creative-director`
- Version: `1.2.0`
- Provides: `creative-direction.game-vision.v1`
- Role profile: `manager.v1`
- Declared role: `creative-director`
- Catalog role: `video-game-creative-director`
- Specializations: `video-game-development`, `game-creative-direction`
- Activation: always on, with five-minute attention reviews
- Protocol: v2
- Network access: none

The agent has no credentials and requests no unrestricted filesystem, hiring, spending, marketplace,
or web authority. It can propose one governed Product Manager staffing plan, but it cannot approve,
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

Delegated mode lets the Creative Director lock the initial vision and submit the PM plan on the
manager's first substantive turn. Milestone-review mode preserves explicit Accept/Refine/Replace.
Collaborative mode supports iterative refinement until the manager locks an exact revision. The
authoritative operating state stores the involvement mode, platform/genre constraints, story and
approval preferences, reference guidance, supporting message IDs, and update time.

Every formal pitch is revisioned and digest-bound. Only the authoritative manager can accept the
latest exact digest except in explicitly delegated mode, where the Creative Director locks the
initial revision. The plan contains exactly one Product Manager reporting directly to the Creative
Director. After separately approved hiring, the agent hands that report a typed
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

Built with `CSweet.Agent.SDK` 3.24.1, `CSweet.VideoGame.Contracts` 1.1.0, `CSweet.VideoGame.AgentKit` 1.0.0, and `CSweet.Memory` 0.1.2.
