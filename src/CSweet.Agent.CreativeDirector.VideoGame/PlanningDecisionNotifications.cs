using CSweet.Agent.SDK;
using CSweet.WorkManagement.Contracts;

namespace CSweet.Agent.CreativeDirector.VideoGame;

public sealed partial class VideoGameCreativeDirectorAgent
{
    internal static Guid? PlanningDecisionNotificationId(string text)
    {
        const string prefix = "Planning decision ";
        return text.StartsWith(prefix, StringComparison.Ordinal) && text.Length >= prefix.Length + 36 &&
            Guid.TryParseExact(text.Substring(prefix.Length, 36), "D", out var id) ? id : null;
    }

    internal static string? PlanningDecisionNotice(DecisionRecord decision, string businessId) =>
        decision.TypeKey == "video-game.management-direction.v1" && decision.Status == DecisionStatuses.Pending
            ? $"Your direction is needed for the game team's planning: {decision.Summary}\n\n" +
              $"[Review the project decision](/organizations/{businessId}/projects/{decision.WorkstreamId:D})\n\n" +
              $"Decision: {decision.Id:D}. Resolve it with a binding rationale; affected scope remains uncommitted until incorporated into accepted planning."
            : null;

    private async Task RelayPlanningDecisionAsync(DecisionRecord decision, AgentRuntimeContext context, CancellationToken token)
    {
        var notice = PlanningDecisionNotice(decision, context.BusinessId);
        if (notice is null || !Guid.TryParse(context.Identity?.ManagerEmployeeId, out var manager)) return;
        var current = await ReadStateAsync(context, token, decision.WorkstreamId);
        if (current.State.WorkstreamId != decision.WorkstreamId) return;
        await context.Platform.Communication.SendDirectMessageAsync(manager, notice,
            $"creative-planning-decision:{decision.Id:N}",
            ProjectWorkContext(current.State, context, decision.Id), token);
    }
}
