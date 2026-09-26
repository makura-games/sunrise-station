using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Movement.Components;

/// <summary>
/// A special jetpack component for borgs that initializes its action on startup
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BorgJetpackComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? JetpackUser;

    [ViewVariables(VVAccess.ReadWrite), DataField("moleUsage")]
    public float MoleUsage = 0.012f;

    [DataField]
    public EntProtoId ToggleAction = "ActionToggleJetpack";

    [DataField, AutoNetworkedField]
    public EntityUid? ToggleActionEntity;

    [ViewVariables(VVAccess.ReadWrite), DataField("acceleration"), AutoNetworkedField]
    public float Acceleration = 1f;

    [ViewVariables(VVAccess.ReadWrite), DataField("friction"), AutoNetworkedField]
    public float Friction = 0.25f;

    [ViewVariables(VVAccess.ReadWrite), DataField("weightlessModifier"), AutoNetworkedField]
    public float WeightlessModifier = 1.2f;
}
