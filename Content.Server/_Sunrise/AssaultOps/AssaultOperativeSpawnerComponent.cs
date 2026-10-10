using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.AssaultOps;

[RegisterComponent]
public sealed partial class AssaultOperativeSpawnerComponent : Component
{
    [DataField("rolePrototype", required: true)]
    public ProtoId<AntagPrototype> OperativeRolePrototype;

    [DataField("startingGearPrototype", required: true)]
    public ProtoId<StartingGearPrototype> OperativeStartingGear;
}
