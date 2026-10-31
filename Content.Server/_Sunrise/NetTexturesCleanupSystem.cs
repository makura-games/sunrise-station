using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server._Sunrise;

/// <summary>
/// A small system that handles event-based cleanup for the NetTexturesManager.
/// Since NetTexturesManager is a standalone manager, it cannot safely subscribe to broadcast events.
/// </summary>
public sealed partial class NetTexturesCleanupSystem : EntitySystem
{
    [Dependency] private NetTexturesManager _netTexturesManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _netTexturesManager.ClearRoundCaches();
    }
}
