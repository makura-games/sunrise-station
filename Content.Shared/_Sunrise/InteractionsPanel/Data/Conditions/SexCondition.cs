using Content.Shared.Humanoid;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.InteractionsPanel.Data.Conditions;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class SexCondition : IAppearCondition
{
    [DataField]
    public bool CheckInitiator { get; private set; } = false;

    [DataField]
    public bool CheckTarget { get; private set; } = true;

    [DataField]
    public Sex RequiredSex { get; private set; } = Sex.Unsexed;

    public bool IsMet(EntityUid initiator, EntityUid target, EntityManager entityManager)
    {
        if (CheckInitiator)
        {
            if (!entityManager.TryGetComponent<HumanoidProfileComponent>(initiator, out var initiatorAppearance))
                return false;

            if (initiatorAppearance.Sex != RequiredSex)
                return false;
        }

        if (CheckTarget)
        {
            if (!entityManager.TryGetComponent<HumanoidProfileComponent>(target, out var targetAppearance))
                return false;

            if (targetAppearance.Sex != RequiredSex)
                return false;
        }

        return true;
    }
}
