using System.Threading.Tasks;
using Content.Server.Database;
using Content.Sunrise.Interfaces.Server;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.IoC;
using Robust.Shared.Network;
using Robust.Shared.Player;
using RobustTimer = Robust.Shared.Timing.Timer;

namespace Content.Server._Sunrise.GameTicking;

public sealed partial class SunriseServerJoinPipelineSystem : EntitySystem
{
    private const string JoinQueueFailureDisconnectReason = "Unexpected join queue error. Please reconnect.";

    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private UserDbDataManager _userDb = default!;
    [Dependency] private ILogManager _logManager = default!;

    private ISawmill? _sawmill;

    private readonly HashSet<NetUserId> _sentToJoinPipeline = [];
    private readonly HashSet<NetUserId> _startedUserDbLoads = [];

    public override void Initialize()
    {
        _sawmill = _logManager.GetSawmill("sunrise.join_pipeline");
    }

    public static bool IsJoinQueueEnabled(IServerJoinQueueManager? joinQueueManager)
    {
        return joinQueueManager?.IsEnabled == true;
    }

    public bool IsJoinQueueEnabled()
    {
        return IoCManager.Instance?.TryResolveType<IServerJoinQueueManager>(out var joinQueueManager) == true &&
               IsJoinQueueEnabled(joinQueueManager);
    }

    public bool SendToJoinPipeline(ICommonSession session)
    {
        return SendToJoinPipeline(session, ResolveJoinQueueManager());
    }

    public bool SendToJoinPipeline(ICommonSession session, IServerJoinQueueManager? joinQueueManager)
    {
        if (!_sentToJoinPipeline.Add(session.UserId))
            return false;

        if (IsJoinQueueEnabled(joinQueueManager))
        {
            StartUserDbLoad(session);
            _ = HandleReadyToJoinQueue(joinQueueManager!, session);
            return true;
        }

        RobustTimer.Spawn(0, () =>
        {
            if (session.Status == SessionStatus.Disconnected)
                return;

            _player.JoinGame(session);
        });

        return true;
    }

    private async Task HandleReadyToJoinQueue(IServerJoinQueueManager joinQueueManager, ICommonSession session)
    {
        try
        {
            await joinQueueManager.HandleReadyToJoin(session);
        }
        catch (Exception ex)
        {
            _sawmill?.Error("Join queue failed while handling ready-to-join session {Session}: {Message}", session, ex.Message);
            StopUserDbLoad(session);

            if (session.Status != SessionStatus.Disconnected)
                session.Channel.Disconnect(JoinQueueFailureDisconnectReason);
        }
    }

    public bool StartUserDbLoadIfJoinQueueDisabled(ICommonSession session)
    {
        return StartUserDbLoadIfJoinQueueDisabled(session, ResolveJoinQueueManager());
    }

    public bool StartUserDbLoadIfJoinQueueDisabled(ICommonSession session, IServerJoinQueueManager? joinQueueManager)
    {
        if (IsJoinQueueEnabled(joinQueueManager))
            return false;

        return StartUserDbLoad(session);
    }

    public bool StartUserDbLoad(ICommonSession session)
    {
        if (!_startedUserDbLoads.Add(session.UserId))
            return false;

        _userDb.ClientConnected(session);
        return true;
    }

    public bool StopUserDbLoad(ICommonSession session)
    {
        _sentToJoinPipeline.Remove(session.UserId);

        if (!_startedUserDbLoads.Remove(session.UserId))
            return false;

        _userDb.ClientDisconnected(session);
        return true;
    }

    private static IServerJoinQueueManager? ResolveJoinQueueManager()
    {
        return IoCManager.Instance?.TryResolveType<IServerJoinQueueManager>(out var joinQueueManager) == true
            ? joinQueueManager
            : null;
    }
}
