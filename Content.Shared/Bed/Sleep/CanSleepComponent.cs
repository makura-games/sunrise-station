using Robust.Shared.GameStates;

namespace Content.Shared.Bed.Sleep;

[NetworkedComponent, RegisterComponent]
[AutoGenerateComponentState]
public sealed partial class CanSleepComponent : Component
{
    [DataField]
    [AutoNetworkedField]
    public EntityUid? SleepAction;
}
