using Content.Client.Overlays;
using Content.Shared._Sunrise.Silicons.StationAi;
using Content.Shared.Standing;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.Silicons.StationAi;

public sealed partial class StationAiBodySystem : EntitySystem
{
    [Dependency] private ShowJobIconsSystem _showJobIcons = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly ProtoId<JobIconPrototype> StationAiBodyJobIcon = "JobIconStationAi";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationAiBodyControllerComponent, AfterAutoHandleStateEvent>(OnControllerState);
        SubscribeLocalEvent<StationAiBodyComponent, GetStatusIconsEvent>(OnGetStatusIcons);
    }

    /// <summary>
    /// Refreshes an open body selection UI after controller state is received from the server.
    /// </summary>
    private void OnControllerState(Entity<StationAiBodyControllerComponent> stationAi, ref AfterAutoHandleStateEvent args)
    {
        if (_ui.TryGetOpenUi<StationAiBodyBoundUserInterface>((EntityUid) stationAi, StationAiBodyUiKey.Key, out var bui))
            bui.Update();
    }

    /// <summary>
    /// Displays the station AI job status icon on an actively controlled AI body when the job HUD is active.
    /// </summary>
    private void OnGetStatusIcons(Entity<StationAiBodyComponent> ent, ref GetStatusIconsEvent args)
    {
        if (!_showJobIcons.IsActive)
            return;

        if (_standing.IsDown(ent.Owner))
            return;

        if (ent.Comp.LinkedAi == null)
            return;

        if (ProtoMan.Resolve(StationAiBodyJobIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}
