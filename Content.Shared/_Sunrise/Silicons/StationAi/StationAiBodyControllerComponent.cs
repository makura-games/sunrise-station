using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Silicons.StationAi;

/// <summary>
/// Tracks which station AI body is currently controlled by this AI brain.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class StationAiBodyControllerComponent : Component
{
    /// <summary>
    /// Current borg chassis controlled by the AI, or null while the AI is in its regular brain/core.
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public EntityUid? CurrentBody;

    /// <summary>
    /// Current body selector rows shown by the component-backed BUI.
    /// </summary>
    [AutoNetworkedField, ViewVariables]
    public List<StationAiBodyEntry> Bodies = [];

    /// <summary>
    /// Action prototype granted to the station AI brain to open the body selector UI.
    /// </summary>
    [DataField]
    public EntProtoId BodyMenuActionPrototype = "ActionStationAiBodyMenu";

    /// <summary>
    /// Granted action entity used by the AI brain to open the body selector UI.
    /// </summary>
    [ViewVariables]
    public EntityUid? BodyMenuAction;
}
