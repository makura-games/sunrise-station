using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Robust.Shared.Asynchronous;

namespace Content.Server.Administration.Logs;

/// <summary>
///     For system events that the manager needs to know about.
///     <see cref="IAdminLogManager"/> for admin log usage.
/// </summary>
public sealed partial class AdminLogSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogs = default!;
    [Dependency] private ITaskManager _task = default!; // Sunrise added - для адекватного завершения отправки админлогов в связке с VictoriaLogs

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(ev => _adminLogs.RoundStarting(ev.Id));
        SubscribeLocalEvent<GameRunLevelChangedEvent>(ev => _adminLogs.RunLevelChanged(ev.New));
    }

    public override void Update(float frameTime)
    {
        _adminLogs.Update();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _task.BlockWaitOnTask(_adminLogs.Shutdown()); // Sunrise edit - для адекватного завершения отправки админлогов в связке с VictoriaLogs
    }
}
