using Content.Server._Sunrise.Antags.Vampires.Systems;

namespace Content.Server._Sunrise.Objectives.Components;

[RegisterComponent, Access(typeof(VampireSystem))]
public sealed partial class BloodDrainConditionComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float BloodDranked = 0f;
}
