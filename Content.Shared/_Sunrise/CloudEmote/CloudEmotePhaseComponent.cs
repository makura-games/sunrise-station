using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Sunrise.CloudEmote;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CloudEmotePhaseComponent : Component
{
    [DataField("player"), ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public EntityUid? Player;
}
