using Content.Shared.NPC.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.AssaultOps;

[RegisterComponent, Access(typeof(AssaultOpsRuleSystem))]
public sealed partial class AssaultOpsRuleComponent : Component
{
    [DataField]
    public EntProtoId IcarusKeyImplant = "IcarusKey";

    [DataField]
    public int RequiredKeys = 3;

    [DataField]
    public ProtoId<JobPrototype>[] KeysCarrierJobs =
    [
        "Captain",
        "HeadOfSecurity",
        "ChiefEngineer",
        "ChiefMedicalOfficer",
        "ResearchDirector",
        "Quartermaster",
    ];

    [DataField(required: true)]
    public ProtoId<NpcFactionPrototype> Faction = default!;

    [DataField]
    public int TcAmountPerOperative = 50;

    public int RoundstartOperatives;

    public EntityUid? UplinkEnt;

    [DataField]
    public SoundSpecifier? GreetingSound = new SoundPathSpecifier("/Audio/_Sunrise/AssaultOperatives/assault_operatives_greet.ogg",
        AudioParams.Default.WithVolume(-6f));

    public WinType WinType = WinType.Stalemate;

    public List<WinCondition> WinConditions = [];

    public EntityUid? ShuttleGrid;

    public EntityUid? TargetStation;
}

public enum WinType : byte
{
    /// <summary>
    ///     Operative major win. Goldeneye activated and all ops alive.
    /// </summary>
    OpsMajor,
    /// <summary>
    ///     Minor win. Goldeneye was activated and some ops alive.
    /// </summary>
    OpsMinor,
    /// <summary>
    ///     Hearty. Goldeneye activated but no ops alive.
    /// </summary>
    Hearty,
    /// <summary>
    ///     Stalemate. Goldeneye not activated and ops still alive.
    /// </summary>
    Stalemate,
    /// <summary>
    ///     Crew major win. Goldeneye not activated and no ops alive.
    /// </summary>
    CrewMajor
}

public enum WinCondition
{
    IcarusActivated,
    AllOpsDead,
    SomeOpsAlive,
    AllOpsAlive
}
