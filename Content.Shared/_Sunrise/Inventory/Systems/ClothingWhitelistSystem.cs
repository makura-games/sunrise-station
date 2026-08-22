using Content.Shared.Inventory.Events;
using Content.Shared.Whitelist;
using Content.Shared._Sunrise.Inventory.Components;

namespace Content.Shared._Sunrise.Inventory.Systems;

public sealed partial class ClothingWhitelistSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingWhitelistComponent, BeingEquippedAttemptEvent>(OnEquippedAttempt);
    }

    private void OnEquippedAttempt(Entity<ClothingWhitelistComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (_whitelistSystem.CheckBoth(args.EquipTarget, ent.Comp.Blacklist, ent.Comp.Whitelist))
            return;

        args.Cancel();
    }
}
