using Content.Shared._Sunrise.FleshCult;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.FleshCult.GameRule;

[RegisterComponent, Access(typeof(FleshCultRuleSystem))]
public sealed partial class FleshCultRuleComponent : Component
{
    public EntityUid? CultistsLeaderMind;

    public SoundSpecifier AddedSound = new SoundPathSpecifier(
        "/Audio/_Sunrise/FleshCult/flesh_culstis_greeting.ogg");

    [DataField]
    public ProtoId<AntagPrototype> FleshCultistPrototypeId = "FleshCultist";

    [DataField("fleshCultistLeaderPrototypeID")]
    public ProtoId<AntagPrototype> FleshCultistLeaderPrototypeId = "FleshCultistLeader";

    [DataField(required: true)]
    public ProtoId<NpcFactionPrototype> Faction;

    public List<EntityUid> Cultists = new();
    public int TotalCultists => Cultists.Count;

    public readonly List<string> CultistsNames = new();

    public Dictionary<EntityUid, FleshHeartStatus> FleshHearts = new();

    public EntityUid? TargetStation;
}
