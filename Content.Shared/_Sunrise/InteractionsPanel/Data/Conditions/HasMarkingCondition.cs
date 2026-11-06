using Content.Shared.Body;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.InteractionsPanel.Data.Conditions;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class HasMarkingCondition : IAppearCondition
{
    [DataField]
    public bool CheckInitiator { get; private set; }

    [DataField]
    public bool CheckTarget { get; private set; } = true;

    [DataField(required: true)]
    public List<string> MarkingWhitelist { get; private set; } = new();

    public bool IsMet(EntityUid initiator, EntityUid target, EntityManager entMan)
    {
        if (CheckInitiator && !HasAnyMarking(initiator, entMan))
            return false;

        if (CheckTarget && !HasAnyMarking(target, entMan))
            return false;

        return true;
    }

    private bool HasAnyMarking(EntityUid uid, EntityManager entMan)
    {
        var visualBody = entMan.System<SharedVisualBodySystem>();
        if (!visualBody.TryGatherMarkingsData(uid, null, out _, out _, out var applied))
            return false;

        foreach (var layerMap in applied.Values)
        {
            foreach (var markingList in layerMap.Values)
            {
                foreach (var marking in markingList)
                {
                    if (MarkingWhitelist.Contains(marking.MarkingId))
                        return true;
                }
            }
        }

        return false;
    }
}
