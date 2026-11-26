using Content.Shared.Alert;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(true, fieldDeltas: true), AutoGenerateComponentPause]
public sealed partial class VampireComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<FactionIconPrototype> StatusIcon = "VampireMasterIcon";

    [DataField, AutoNetworkedField]
    public ProtoId<AlertPrototype> BloodAlert = "VampireBlood";

    /// <summary>
    /// Предел отображения, не самого запаса крови.
    /// </summary>
    public const int MaxDisplayedBlood = 9999;

    /// <summary>
    /// Расходуемый запас крови.
    /// </summary>
    [AutoNetworkedField]
    public int DrunkBlood;

    [AutoNetworkedField]
    public bool FangsExtended;

    /// <summary>
    /// Сытость; при нуле вампир замедляется и теряет запас крови.
    /// </summary>
    [AutoNetworkedField]
    public float BloodFullness = 90f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextBloodUpdate;

    [AutoNetworkedField, AutoPausedField]
    public TimeSpan LastBloodUpdate;

    /// <summary>
    /// Дробный остаток расхода крови при голоде.
    /// </summary>
    [AutoNetworkedField]
    public float StarvationDrunkBloodDrainAccumulator;

    /// <summary>
    /// Множитель скорости ходьбы при нулевой сытости.
    /// </summary>
    [DataField]
    public float StarvationWalkSpeedModifier = 0.7f;

    /// <summary>
    /// Множитель скорости бега при нулевой сытости.
    /// </summary>
    [DataField]
    public float StarvationSprintSpeedModifier = 0.7f;

    [DataField, AutoNetworkedField]
    public VampirePowerLevel PowerLevel = VampirePowerLevel.Neonate;

}

public enum VampireVisualLayers : byte
{
    Digit1,
    Digit2,
    Digit3,
    Digit4,
}
