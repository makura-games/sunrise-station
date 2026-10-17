using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.NightVision;
using Content.Shared.Overlays;

namespace Content.Shared._Sunrise.NightVision;

/// <summary>
/// Связывает переключатель ПНВ с ночным зрением Wizden на самом предмете.
/// </summary>
public sealed partial class NightVisionItemToggleSystem : EntitySystem
{
    [Dependency] private SharedNightVisionSystem _nightVision = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NightVisionComponent, ItemToggledEvent>(OnItemToggled);
    }

    private void OnItemToggled(Entity<NightVisionComponent> ent, ref ItemToggledEvent args)
    {
        if (!ent.Comp.RelayOverlay)
            return;

        _nightVision.SetEnabled(ent.AsNullable(), args.Activated);
    }
}
