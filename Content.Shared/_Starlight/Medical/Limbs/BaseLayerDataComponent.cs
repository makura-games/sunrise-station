using Content.Shared.Humanoid;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Shared.Starlight;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BaseLayerDataComponent : Component
{
    /// <summary>
    /// Replacement layer data applied while this limb is attached.
    /// </summary>
    [DataField, AutoNetworkedField]
    public PrototypeLayerData? Data;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BaseLayerToggledDataComponent : Component
{
    /// <summary>
    /// Replacement layer data applied while this limb is toggled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public PrototypeLayerData? Data;
}
