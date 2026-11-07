using Content.Shared.Procedural;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Salvage.Expeditions.Modifiers;

public sealed partial class SalvageTemperatureMod
{
    [DataField]
    public List<ProtoId<SalvageDifficultyPrototype>>? Difficulties { get; private set; }
}
