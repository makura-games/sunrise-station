using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.BloodCult.Components;

[RegisterComponent]
public sealed partial class ConstructShellComponent : Component
{
    public readonly string ShardSlotId = "Shard";

    [DataField]
    public List<EntProtoId> ConstructForms = [];

    [DataField("shardSlot", required: true)]
    public ItemSlot ShardSlot = new();
}
