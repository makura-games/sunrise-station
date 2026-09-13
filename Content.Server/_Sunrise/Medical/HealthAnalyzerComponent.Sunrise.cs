using Content.Shared.Damage.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.List;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Medical.Components;

public sealed partial class HealthAnalyzerComponent
{
    [DataField(customTypeSerializer: typeof(PrototypeIdListSerializer<DamageContainerPrototype>))]
    public List<string>? DamageContainers;
}
