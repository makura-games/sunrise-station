using Content.Server.Hands.Systems;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.HTN;
using Content.Server.NPC;
using Content.Shared.Weapons.Ranged.Components;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Hands.Components;
using Robust.Shared.GameObjects;

namespace Content.Server._Sunrise.NPC.HTN;

/// <summary>
/// Переключает состояние затвора (по сути закрывая его, если он открыт) у огнестрельного оружия в руках NPC
/// </summary>
public sealed partial class CloseBoltOperator : HTNOperator
{
    [Dependency] private IEntityManager _entManager = default!;
    private GunSystem _gunSystem = default!;

    [DataField("shutdownState")]
    public HTNPlanState ShutdownState { get; private set; } = HTNPlanState.TaskFinished;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _gunSystem = sysManager.GetEntitySystem<GunSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<HandsComponent>(owner, out var hands))
            return HTNOperatorStatus.Failed;

        var handsSystem = _entManager.System<HandsSystem>();

        var heldEntity = handsSystem.GetActiveItem((owner, hands));

        if (heldEntity == null)
            return HTNOperatorStatus.Failed;

        if (!_entManager.TryGetComponent<ChamberMagazineAmmoProviderComponent>(heldEntity, out var chamber))
            return HTNOperatorStatus.Failed;

        if (chamber.BoltClosed != false)
        {
            return HTNOperatorStatus.Finished;
        }

        _gunSystem.ToggleBolt(heldEntity.Value, chamber, owner);

        return HTNOperatorStatus.Finished;
    }
}
