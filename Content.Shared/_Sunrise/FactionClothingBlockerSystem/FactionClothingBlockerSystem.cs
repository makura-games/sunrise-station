using Content.Shared._Sunrise.Biocode;
using Content.Shared.Inventory.Events;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.FactionClothingBlockerSystem;

public sealed partial class FactionClothingBlockerSystem : EntitySystem
{
    [Dependency] private BiocodeSystem _biocodeSystem = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FactionClothingBlockerComponent, BeingEquippedAttemptEvent>(OnEquippedAttempt);
    }

    private void OnEquippedAttempt(EntityUid uid, FactionClothingBlockerComponent component, BeingEquippedAttemptEvent args)
    {
        // Does not work correctly on the client due to poor serialization of NpcFactionMemberComponent
        if (_net.IsClient)
            return;

        if (TryComp<BiocodeComponent>(args.Equipment, out var biocodedComponent))
        {
            if (!_biocodeSystem.CanUse(args.EquipTarget, biocodedComponent.Factions))
                args.Cancel();
        }
    }
}
