using System.Threading.Tasks;
using Content.Server._Sunrise.GameTicking;
using Robust.Shared.Player;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    [Dependency] private SunriseServerJoinPipelineSystem _sunriseServerJoinPipeline = default!;

    private void SendToJoinPipeline(ICommonSession session)
    {
        _sunriseServerJoinPipeline.SendToJoinPipeline(session);
    }

    private async Task<bool> CanPassSunriseJoinGateAsync(ICommonSession session)
    {
        var ev = new SunriseJoinGateConnectedEvent(session);
        RaiseLocalEvent(ev);

        return ev.GateTask == null || await ev.GateTask;
    }

    internal bool TryEnterGameAfterSunriseGate(ICommonSession session)
    {
        var ev = new SunriseJoinGateEnterGameEvent(session);
        RaiseLocalEvent(ev);
        if (ev.Cancelled)
            return false;

        StartUserDbLoadIfJoinQueueDisabled(session);
        return true;
    }

    private bool StartUserDbLoadIfJoinQueueDisabled(ICommonSession session)
    {
        return _sunriseServerJoinPipeline.StartUserDbLoadIfJoinQueueDisabled(session);
    }

    private void StopUserDbLoad(ICommonSession session)
    {
        _sunriseServerJoinPipeline.StopUserDbLoad(session);
    }
}
