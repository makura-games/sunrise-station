using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Overlays;
using Content.Shared.Standing;
using Content.Shared.StatusIcon.Components;

namespace Content.Client.Overlays;

public sealed partial class ShowHungerIconsSystem : EquipmentHudSystem<ShowHungerIconsComponent>
{
    [Dependency] private HungerSystem _hunger = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HungerComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
    }

    private void OnGetStatusIconsEvent(EntityUid uid, HungerComponent component, ref GetStatusIconsEvent ev)
    {
        if (!IsActive)
            return;

        if (!_standing.IsDown(uid))
            return;

        if (_hunger.TryGetStatusIconPrototype(component, out var iconPrototype))
            ev.StatusIcons.Add(iconPrototype);
    }
}
