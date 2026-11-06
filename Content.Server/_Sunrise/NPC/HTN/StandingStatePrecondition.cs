using Content.Server.NPC.HTN.Preconditions;
using Content.Server.NPC;
using Content.Shared.Standing;

namespace Content.Server._Sunrise.NPC.HTN;

public sealed partial class StandingStatePrecondition : HTNPrecondition
{
    [Dependency] private IEntityManager _entManager = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("isStanding")]
    public bool IsStanding = true;

    public override bool IsMet(NPCBlackboard blackboard)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!_entManager.TryGetComponent<StandingStateComponent>(owner, out var standing))
            return false;

        return IsStanding && standing.Standing ||
               !IsStanding && !standing.Standing;
    }
}
