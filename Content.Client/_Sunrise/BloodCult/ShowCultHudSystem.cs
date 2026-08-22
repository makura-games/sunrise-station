using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared.StatusIcon.Components;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.BloodCult;

public sealed partial class ShowCultHudSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodCultistComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    private void OnGetStatusIconsEvent(EntityUid uid,
        BloodCultistComponent bloodCultistComponent,
        ref GetStatusIconsEvent args)
    {
        var ent = _player.LocalSession?.AttachedEntity;
        if (!HasComp<BloodCultistComponent>(ent))
            return;

        if (_prototype.TryIndex(bloodCultistComponent.StatusIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}
