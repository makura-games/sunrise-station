using Content.Shared.Humanoid;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.InteractionsPanel.Data.Conditions;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class SpeciesCondition : IAppearCondition
{
    [DataField]
    public bool CheckInitiator { get; private set; }

    [DataField]
    public bool CheckTarget { get; private set; } = true;

    [DataField(required: true)]
    public HashSet<string> AllowedSpecies { get; private set; } = new();

    public bool IsMet(EntityUid initiator, EntityUid target, EntityManager entMan)
    {
        if (CheckInitiator && !CheckSpecies(initiator, entMan))
            return false;

        if (CheckTarget && !CheckSpecies(target, entMan))
            return false;

        return true;
    }

    private bool CheckSpecies(EntityUid uid, EntityManager entMan)
    {
        if (!entMan.TryGetComponent<HumanoidProfileComponent>(uid, out var appearance))
            return false;

        return AllowedSpecies.Contains(appearance.Species);
    }
}
