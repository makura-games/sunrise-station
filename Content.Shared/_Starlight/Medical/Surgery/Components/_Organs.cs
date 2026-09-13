using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
namespace Content.Shared.Starlight.Medical.Surgery.Steps.Parts;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class EyeImplantComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class OrganBrainComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class OrganAppendixComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class OrganEarsComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class OrganHeartComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class OrganStomachComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class OrganLiverComponent : Component;
[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))] public sealed partial class OrganKidneysComponent : Component;
[RegisterComponent, NetworkedComponent]
public sealed partial class OrganTongueComponent : Component
{
    [DataField]
    public bool IsMuted;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class OrganEyesComponent : Component
{
    [DataField]
    public int? EyeDamage;
    [DataField]
    public int? MinDamage;
}
[RegisterComponent, NetworkedComponent]
public sealed partial class OrganVisualizationComponent : Component
{
    /// <summary>
    /// Body visual layer affected by this organ.
    /// </summary>
    [DataField]
    public HumanoidVisualLayers Layer;

    /// <summary>
    /// Replacement layer data applied while this organ is implanted.
    /// </summary>
    [DataField]
    public PrototypeLayerData Data = new();
}

[RegisterComponent, NetworkedComponent, Access(typeof(SharedSurgerySystem))]
public sealed partial class FunctionalOrganComponent : Component
{
    [DataField("comps")]
    public ComponentRegistry? Components;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class OrganDamageComponent : Component
{
    [DataField]
    public DamageSpecifier? Damage;
}
