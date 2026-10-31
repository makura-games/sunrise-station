using Content.Server.Hands.Systems;
using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Hands.Components;

namespace Content.Server._Sunrise.NPC.HTN;

public sealed partial class IsBoltOpenPrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        if (!_entManager.TryGetComponent<HandsComponent>(owner, out var hands))
            return false;

        var handsSystem = _entManager.System<HandsSystem>();

        var heldEntity = handsSystem.GetActiveItem((owner, hands));

        if (!_entManager.TryGetComponent<ChamberMagazineAmmoProviderComponent>(heldEntity, out var chamber))
            return false;

        return chamber.BoltClosed == false;
    }
}
