using Content.Server.Maps;
using Content.Shared.Maps;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.StationCentComm;

[RegisterComponent]
public sealed partial class StationCentCommComponent : Component
{
    [DataField(required: true)]
    public ProtoId<GameMapPrototype> Station;

    [DataField]
    public EntityUid Entity = EntityUid.Invalid;

    [DataField]
    public EntityWhitelist? ShuttleWhitelist;

    public MapId MapId = MapId.Nullspace;
}
