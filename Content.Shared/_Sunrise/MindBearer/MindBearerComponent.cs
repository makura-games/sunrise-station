using Content.Shared.Ghost.Roles.Raffles;
using Content.Shared.Whitelist;

using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.MindBearer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class MindBearerComponent : Component
{
    [DataField, AutoNetworkedField]
    public int UsesLeft = 1;

    [DataField]
    public TimeSpan UseTime = TimeSpan.FromSeconds(5);

    [DataField]
    public EntityWhitelist? WhitelistTargets;

    [DataField]
    public EntityWhitelist? BlacklistTargets;

    [DataField]
    public GhostRoleRaffleSettings GhostRoleSettings = new()
    {
        InitialDuration = 10,
        JoinExtendsDurationBy = 10,
        MaxDuration = 30
    };
}
