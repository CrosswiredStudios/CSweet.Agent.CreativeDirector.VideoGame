using CSweet.Agent.SDK;

namespace CSweet.Agent.CreativeDirector.VideoGame;

public sealed partial class VideoGameCreativeDirectorAgent
{
    internal static async Task CheckProducerHandoffAsync(CreativeDirectorOperatingState state,
        AgentRuntimeContext context, CancellationToken token)
    {
        if (state.HandoffSessionId is not { } id) return;
        var session = await context.Platform.Communication.ReadCoordinationAsync(id, token);
        if (session.Status is "Failed" or "Blocked" or "Cancelled")
            throw new InvalidOperationException(
                $"Producer handoff {id:D} is {session.Status}. " +
                ReconcileStallPolicy.Summarize(session.FinalSummary) +
                " Eligible execution failures resume on a ready replacement runtime with bounded retries; persistent failures require manager review in Communications.");
    }
}
