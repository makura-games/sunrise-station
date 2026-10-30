using Content.Shared.Procedural;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Salvage.Expeditions;

public sealed partial class SalvageFactionPrototype
{
    [DataField]
    public List<ProtoId<SalvageDifficultyPrototype>>? Difficulties { get; private set; }
}
