using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.BloodCult.UI;

[Serializable, NetSerializable]
public sealed class CultistFactoryBUIState : BoundUserInterfaceState
{
    public CultistFactoryBUIState(Dictionary<string, List<EntProtoId>> ids)
    {
        Ids = ids;
    }

    public Dictionary<string, List<EntProtoId>> Ids { get; set; }
}

[Serializable, NetSerializable]
public sealed class CultistFactoryItemSelectedMessage : BoundUserInterfaceMessage
{
    public CultistFactoryItemSelectedMessage(List<EntProtoId> equipment)
    {
        Equipment = equipment;
    }

    public List<EntProtoId> Equipment { get; private set; }
}
