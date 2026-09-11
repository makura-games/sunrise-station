using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

//namespace Content.Shared.NPC.Components;
namespace Content.Shared._Sunrise.NoTarget.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NpcNoTargetComponent : Component
{
    /// <summary>
    ///     Текущее состояние активности.
    /// </summary>
    [AutoNetworkedField]
    public bool Enabled = false;

    /// <summary>
    ///     Компоненты, при наличии которых активируется поведение.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityWhitelist? Whitelist = new()
    {
        Components = ["Stealth"]
    };
}
