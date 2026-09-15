using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Smell.Prototypes;

/// <summary>
/// Single tuning config for temporary scent triggers: damage thresholds and durations
/// of wound, poison, other-blood, arousal and orgasm scents — balanceable without recompiling.
/// </summary>
[Prototype]
public sealed partial class SmellSystemConfigPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Accumulated slash/pierce damage above which the entity starts
    /// smelling of its own blood or bruises.
    /// </summary>
    [DataField]
    public int WoundScentThreshold { get; private set; } = 10;

    /// <summary>
    /// Accumulated poison damage above which the body smells of toxins.
    /// </summary>
    [DataField]
    public int PoisonScentThreshold { get; private set; } = 25;

    /// <summary>
    /// Duration of the scent left by wounds and bruises.
    /// </summary>
    [DataField]
    public TimeSpan WoundScentDuration { get; private set; } = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Duration of the poisoning scent.
    /// </summary>
    [DataField]
    public TimeSpan PoisonScentDuration { get; private set; } = TimeSpan.FromSeconds(200);

    /// <summary>
    /// Duration of the victim-blood scent applied to an attacker finishing off a critical target.
    /// </summary>
    [DataField]
    public TimeSpan OtherBloodScentDuration { get; private set; } = TimeSpan.FromSeconds(600);

    /// <summary>
    /// Duration of the arousal scent (on oneself).
    /// </summary>
    [DataField]
    public TimeSpan ArousalScentDuration { get; private set; } = TimeSpan.FromSeconds(300);

    /// <summary>
    /// Duration of the orgasm scent (on both participants).
    /// </summary>
    [DataField]
    public TimeSpan OrgasmScentDuration { get; private set; } = TimeSpan.FromSeconds(500);

    /// <summary>
    /// Range (in meters) within which scents can be washed off a target.
    /// </summary>
    [DataField]
    public float ScentCleaningRange { get; private set; } = 1.5f;

    /// <summary>
    /// Tooltip color shown while the target's base scent is masked by soap.
    /// </summary>
    [DataField]
    public Color MaskedScentColor { get; private set; } = Color.FromHex("#a6d8ff");
}
