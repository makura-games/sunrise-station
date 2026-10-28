using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules.Components;

public sealed partial class ZombieRuleComponent
{
    /*
     * Sunrise-часть компонента хранит пороги заражения и состояние одноразовых реакций.
     */

    /// <summary>
    /// After this amount of the crew become zombies, the CBURN shuttle game rule will be started.
    /// </summary>
    [DataField]
    public float ZombieCburnCallPercentage = 0.65f;

    /// <summary>
    /// The CBURN shuttle game rule that is started when <see cref="ZombieCburnCallPercentage"/> is reached.
    /// </summary>
    [DataField]
    public EntProtoId ZombieCburnGameRule = "ERTShuttleCBURNSmall";

    /// <summary>
    /// After this amount of the crew become zombies, the Icarus beam will be fired at the station.
    /// </summary>
    [DataField]
    public float ZombieIcarusBeamPercentage = 0.9f;

    /// <summary>
    /// Tracks whether the CBURN shuttle game rule has already been called.
    /// </summary>
    public bool CburnCalled;

    /// <summary>
    /// Tracks whether the Icarus beam has already been fired.
    /// </summary>
    public bool IcarusBeamCalled;
}
