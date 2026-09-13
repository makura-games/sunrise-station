using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared.Mech.Components;

public sealed partial class MechComponent
{
    /// <summary>
    /// Отключается ли мех после применения emag.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public bool BreakOnEmag = true;

    /// <summary>
    /// Включены ли фары меха.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public bool Lights;

    /// <summary>
    /// Сущности, которым запрещено управлять мехом.
    /// </summary>
    [DataField]
    public EntityWhitelist? PilotBlacklist;

    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public MechHealthState HealthState = MechHealthState.Normal;

    #region Messages

    [DataField]
    public string MessageHello = "mech-message-hello";

    [DataField]
    public string MessageGoodbye = "mech-message-goodbye";

    [DataField]
    public string MessageEnableLight = "mech-message-enable-light";

    [DataField]
    public string MessageDisableLight = "mech-message-disable-light";

    [DataField]
    public string MessageAlert50 = "mech-message-alert_50";

    [DataField]
    public string MessageAlert25 = "mech-message-alert-25";

    [DataField]
    public string MessageAlert5 = "mech-message-alert-5";

    [DataField]
    public string MessageInsertEquipment = "mech-message-insert-equipment";

    [DataField]
    public string MessageRemoveEquipment = "mech-message-remove-equipment";

    [DataField]
    public string MessageCycleEquipment = "mech-message-cycle-equipment";

    #endregion

    [DataField]
    public EntProtoId MechLightsAction = "ActionMechLights";

    [DataField]
    public EntityUid? MechLightsActionEntity;
}

public enum MechHealthState
{
    Normal,
    Healthy,
    Damaged,
    Critical,
}
