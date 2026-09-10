#pragma warning disable IDE0130 // Пространство имён соответствует расширяемому upstream-компоненту.
namespace Content.Server.Communications;

public sealed partial class CommunicationsConsoleComponent
{
    /// <summary>
    /// Alert levels this console may set. A null value allows every crew-selectable level.
    /// </summary>
    [DataField]
    public HashSet<string>? AllowedAlertLevels;

    /// <summary>
    /// Whether this console may set allowed non-selectable levels and bypass alert selection locks.
    /// </summary>
    [DataField]
    public bool ForceAlertLevelChanges;

    /// <summary>
    /// Whether this console may choose which station receives alert-level changes.
    /// </summary>
    [DataField]
    public bool CanSelectAlertStation;

    /// <summary>
    /// Station currently selected for alert-level changes.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? SelectedAlertStation;
}
