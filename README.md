# C-Sweet Video Game Creative Director

Provide durable video game vision and creative leadership from discovery through pitch approval, product-manager handoff, and production oversight.

## Contract

- Package ID: `com.csweet.video-game-creative-director`
- Version: `0.1.0`
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

`Discovery → PitchReview → VisionAccepted → PMPlanPending → PMHiringPending → VisionHandoff → Oversight`

Discovery starts with one structured choice and asks only for missing video-game inputs. PNG, JPEG,
WebP, PDF, UTF-8 text, and Markdown references are passed to the configured model through opaque,
broker-validated media references; agent VMs never receive storage paths or raw storage credentials.

Every formal pitch is revisioned and digest-bound. Only the authoritative manager can accept the
latest exact digest. After acceptance, the agent waits for approval and fulfillment of its Product
Manager plan, hands that direct report a typed `creative-direction.game-vision-brief.v1` artifact,
and enters oversight only after an exact-digest, blocker-free
`product-management.game-vision-acknowledgement.v1` response.

During oversight it answers creative questions, escalates decisions owned by other roles, relays the
manager's answer to the original worker, consumes subordinate status reports, and emits attributed
management reporting without unchanged-state chatter.

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

Built with `CSweet.Agent.SDK` 3.21.0.
