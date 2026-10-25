# Creative Director 4.0.0

Manager-only onboarding, project-scoped GDD authorship, exact creative reviews and explicit independent author review. Uses SDK interaction modes and coordination; no CEO onboarding or hiring authority.

CEO communication and material business decisions belong to the Video Game Director. Specialists preserve independent discipline evidence; no role may manufacture another role's acceptance.


`com.csweet.video-game-creative-director` version **4.0.0** provides shared creative leadership under the Video Game Director. The Producer is a peer responsible for delivery across the game portfolio.

The Creative Director develops approved concepts into creative pillars, player experience, gameplay, presentation, acceptance criteria and a GDD. The Director commissions this work through `video-game.creative-design.request.v1`, bound to the exact CEO-approved pitch and project/team. Generated drafts are retained before document creation. Revision feedback stays in the same document and project, and submissions return exact revision evidence for Director review.

The role handles creative tasks through canonical `work.execution.run` assignments and returns project-scoped creative evidence through `management.check-in.v1`. Chat answers require explicit project context. Missing scope or an unapproved pitch blocks creative execution.

Hiring plans, supervision of the Producer, project setup, budget decisions and official pitch approval belong to the Video Game Director and the CEO. This package requests no resource-change, project-approval or artifact-decision authority and no longer publishes a production profile. Its manifest requests brokered model, calendar, state, document creation/revision/submission, communication and canonical work infrastructure required by the SDK. No direct network access or credentials are requested.

## Verify and release

```powershell
dotnet test -c Release
dotnet run --project src/CSweet.Agent.CreativeDirector.VideoGame -c Release --no-build -- --self-test
dotnet pack src/CSweet.Agent.CreativeDirector.VideoGame -c Release --no-build --no-restore -o .artifacts/packages
```

This breaking role split targets a fresh installation. The former game leadership implementation and regression coverage now live in `CSweet.Agent.Director.VideoGame`. Publish a release and use Installed Agents to apply it; do not patch installed payloads or approved package records.

`video-game.creative-review.request.v1` returns findings bound to the exact project, team, document revision and approved pitch through SDK coordination. Naomi cannot review her own GDD. SDK delegation received/tracking events and attention recovery handle tracked creative requests; no bespoke message-forwarding queue is used.
