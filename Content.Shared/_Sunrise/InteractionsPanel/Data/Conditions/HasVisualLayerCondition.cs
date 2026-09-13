using Content.Shared.Body;
using Content.Shared.Humanoid;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.InteractionsPanel.Data.Conditions;

[Serializable, NetSerializable, DataDefinition]
public sealed partial class HasVisualLayerCondition : IAppearCondition
{
    [DataField]
    public bool CheckInitiator { get; private set; }

    [DataField]
    public bool CheckTarget { get; private set; } = true;

    [DataField(required: true)]
    public HumanoidVisualLayers Layer { get; private set; }

    public bool IsMet(EntityUid initiator, EntityUid target, EntityManager entMan)
    {
        if (CheckInitiator && !CheckLayer(initiator, entMan))
            return false;

        if (CheckTarget && !CheckLayer(target, entMan))
            return false;

        return true;
    }

    private bool CheckLayer(EntityUid uid, EntityManager entMan)
    {
        var visualBody = entMan.System<SharedVisualBodySystem>();
        if (!visualBody.TryGatherMarkingsData(uid, [Layer], out _, out _, out var applied))
            return false;

        foreach (var layerMap in applied.Values)
        {
            if (layerMap.TryGetValue(Layer, out var markings) && markings.Count > 0)
                return true;
        }

        return false;
    }
}
