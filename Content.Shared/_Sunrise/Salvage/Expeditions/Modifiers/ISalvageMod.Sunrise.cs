using Content.Shared.Procedural;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Salvage.Expeditions.Modifiers;

public partial interface ISalvageMod
{
    List<ProtoId<SalvageDifficultyPrototype>>? Difficulties { get; }
}
