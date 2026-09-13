using Content.Shared.Humanoid;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Sunrise.Pacificator;

/// <summary>
///
/// </summary>
[RegisterComponent]
[Access(typeof(PacificatorSystems))]
public sealed partial class PacificatorComponent : Component
{
    // 1% charge per second.
    [ViewVariables(VVAccess.ReadWrite)] [DataField("chargeRate")]
    public float ChargeRate { get; set; } = 0.01f;
    // The gravity generator has two power values.
    // Idle power is assumed to be the power needed to run the control systems and interface.
    [DataField("idlePower")] public float IdlePowerUse { get; set; }
    // Active power is the power needed to keep the gravity field stable.
    [DataField("activePower")] public float ActivePowerUse { get; set; }
    [DataField("lightRadiusMin")] public float LightRadiusMin { get; set; }
    [DataField("lightRadiusMax")] public float LightRadiusMax { get; set; }

    /// <summary>
    /// Is the power switch on?
    /// </summary>
    [DataField("switchedOn")]
    public bool SwitchedOn { get; set; } = true;

    /// <summary>
    /// Is the gravity generator intact?
    /// </summary>
    [DataField("intact")]
    public bool Intact { get; set; } = true;

    [DataField("maxCharge")]
    public float MaxCharge { get; set; } = 1;

    // 0 -> 1
    [ViewVariables(VVAccess.ReadWrite)] [DataField("charge")] public float Charge { get; set; } = 1;

    [ViewVariables]
    public bool Active { get; set; } = false;

    [ViewVariables] public bool NeedUIUpdate { get; set; }

    [ViewVariables(VVAccess.ReadWrite), DataField("nextTick", customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextTick = TimeSpan.Zero;

    public TimeSpan RefreshCooldown = TimeSpan.FromSeconds(5);

    [DataField]
    public float Range = 32f;

    public HashSet<Entity<HumanoidProfileComponent>> PacifiedEntities = [];
}
