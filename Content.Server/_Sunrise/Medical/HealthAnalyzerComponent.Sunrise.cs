using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Medical.Components;

public sealed partial class HealthAnalyzerComponent
{
    [DataField]
    public List<ProtoId<DamageContainerPrototype>>? DamageContainers;
}
