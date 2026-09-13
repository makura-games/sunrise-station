using Content.Shared.Overlays;
using Content.Shared.Security.Components;
using Content.Shared.Standing;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Overlays;

public sealed partial class ShowCriminalRecordIconsSystem : EquipmentHudSystem<ShowCriminalRecordIconsComponent>
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CriminalRecordComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    private void OnGetStatusIconsEvent(EntityUid uid, CriminalRecordComponent component, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        if (_standing.IsDown(uid)) // Sunrise-standing
            return;

        if (_prototype.Resolve(component.StatusIcon, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
    }
}
