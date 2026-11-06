using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Bed;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CanSleepOnBuckleComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<EntityUid, EntityUid> SleepAction = [];
}
