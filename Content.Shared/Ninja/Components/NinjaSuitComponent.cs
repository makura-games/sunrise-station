using Content.Shared.Actions;
using Content.Shared.Ninja.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Ninja.Components;

/// <summary>
/// Component for ninja suit abilities and power consumption.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedNinjaSuitSystem))]
public sealed partial class NinjaSuitComponent : Component
{
    /// <summary>
    /// Sound played when a ninja is hit while cloaked.
    /// </summary>
    [DataField]
    public SoundSpecifier RevealSound = new SoundPathSpecifier("/Audio/Effects/chime.ogg");

    /// <summary>
    /// ID of the use delay to disable all ninja abilities.
    /// </summary>
    [DataField]
    public string DisableDelayId = "suit_powers";

    /// <summary>
    /// The action id for recalling a bound energy katana
    /// </summary>
    [DataField]
    public EntProtoId RecallKatanaAction = "ActionRecallKatana";

    [DataField, AutoNetworkedField]
    public EntityUid? RecallKatanaActionEntity;

    /// <summary>
    /// Battery charge used per tile the katana teleported.
    /// Uses 1% of a default battery per tile.
    /// </summary>
    [DataField]
    public float RecallCharge = 3.6f;

    /// <summary>
    /// The action id for creating an EMP burst
    /// </summary>
    [DataField]
    public EntProtoId EmpAction = "ActionNinjaEmp";

    [DataField, AutoNetworkedField]
    public EntityUid? EmpActionEntity;

    /// <summary>
    /// Battery charge used to create an EMP burst. Can do it 2 times on a small-capacity power cell.
    /// </summary>
    [DataField]
    public float EmpCharge = 180f;

    // TODO: EmpOnTrigger bruh
    /// <summary>
    /// Range of the EMP in tiles.
    /// </summary>
    [DataField]
    public float EmpRange = 6f;

    /// <summary>
    /// Power consumed from batteries by the EMP
    /// </summary>
    [DataField]
    public float EmpConsumption = 100000f;

    /// <summary>
    /// How long the EMP effects last for
    /// </summary>
    [DataField]
    public TimeSpan EmpDuration = TimeSpan.FromSeconds(60);
    // Sunrise-Start
    /// <summary>
    /// The action id for creating a smoke grenade
    /// </summary>
    [DataField]
    public EntProtoId SmokeGrenadeAction = "ActionCreateSmokeGrenade";

    [DataField, AutoNetworkedField]
    public EntityUid? SmokeGrenadeActionEntity;

    /// <summary>
    /// Battery charge used to create a smoke grenade.
    /// </summary>
    [DataField]
    public float SmokeGrenadeCharge = 120f;

    /// <summary>
    /// The action id for creating a flashbang grenade
    /// </summary>
    [DataField]
    public EntProtoId FlashbangGrenadeAction = "ActionCreateFlashbangGrenade";

    [DataField, AutoNetworkedField]
    public EntityUid? FlashbangGrenadeActionEntity;

    /// <summary>
    /// Battery charge used to create a flashbang grenade.
    /// </summary>
    [DataField]
    public float FlashbangGrenadeCharge = 30;
    // Sunrise-End
}

public sealed partial class RecallKatanaEvent : InstantActionEvent;

public sealed partial class NinjaEmpEvent : InstantActionEvent;

public sealed partial class CreateSmokeGrenadeEvent : InstantActionEvent;

public sealed partial class CreateFlashbangGrenadeEvent : InstantActionEvent;
