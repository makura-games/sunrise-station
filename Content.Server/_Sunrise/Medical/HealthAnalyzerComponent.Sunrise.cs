using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Server.Medical.Components;

public sealed partial class HealthAnalyzerComponent
{
    [DataField]
    public List<ProtoId<DamageContainerPrototype>>? DamageContainers;
}
