using Content.Server._Sunrise.GameTicking.Rules;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.GameTicking.Rules.Components;

/// <summary>
/// Stores state for the ninja outpost game rule.
/// Tracks the target station and whether the ninja escaped on their shuttle.
/// </summary>
[RegisterComponent, Access(typeof(NinjaRuleSystem))]
public sealed partial class NinjaRuleComponent : Component
{
    /// <summary>
    /// The station the ninja is operating against.
    /// Set in <see cref="NinjaRuleSystem.Started"/>.
    /// </summary>
    [ViewVariables]
    public EntityUid? TargetStation;

    /// <summary>
    /// Whether the ninja successfully escaped by boarding their shuttle during FTL.
    /// </summary>
    [ViewVariables]
    public bool EscapedOnShuttle;

    /// <summary>
    /// Whether the FTL route to the outpost has been unlocked by completing all objectives.
    /// </summary>
    [ViewVariables]
    public bool FtlUnlocked;

    /// <summary>
    /// The map entity of the outpost, used to enable its FTL destination on completion.
    /// </summary>
    [ViewVariables]
    public EntityUid? OutpostMapEntity;

    /// <summary>
    /// Accumulated frame time used to throttle objective checks.
    /// </summary>
    [ViewVariables]
    public TimeSpan UpdateNextTime;

    /// <summary>
    /// How often (in seconds) to poll objective completion.
    /// </summary>
    [DataField]
    public TimeSpan ObjectiveCheckInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Ninja return objective prototype.
    /// </summary>
    [DataField]
    public EntProtoId ReturnObjectiveProto;

    /// <summary>
    /// Ninja return objective prototype.
    /// </summary>
    [DataField]
    public EntityWhitelist FtlMapWhitelist;
}
