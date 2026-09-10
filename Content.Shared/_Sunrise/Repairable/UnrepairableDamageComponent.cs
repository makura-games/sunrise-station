using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Repairable;

/// <summary>
///     Sunrise added.
///     Marks an entity as having damage types that cannot be repaired using standard Welder/paste tools.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UnrepairableDamageComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<DamageTypePrototype>> Types = new() { "Mangleness", "Deterioration" };
}
