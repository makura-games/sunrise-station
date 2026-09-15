using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Smell.Prototypes;

/// <summary>
/// Maps a status effect (drunkenness, narcotics, etc.) to a condition scent.
/// While the effect is active the bearer emits the scent. Scent strength depends
/// on the effect stage: a fresh effect smells strongest and fades towards its end.
/// </summary>
[Prototype]
public sealed partial class StatusScentPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Status effect entity whose presence enables this scent,
    /// e.g. "StatusEffectDrunk".
    /// </summary>
    [DataField(required: true)]
    public EntProtoId StatusEffect { get; private set; }

    /// <summary>
    /// Group scent emitted by the bearer (alcohol, stimulants, drugs).
    /// </summary>
    [DataField(required: true)]
    public ProtoId<ScentPrototype> Scent { get; private set; }

    /// <summary>
    /// Minimum effect duration for the scent to reach Strong.
    /// Short effects (a sip of booze) must not smell strongly:
    /// their maximum is Medium, fading further for shorter durations.
    /// </summary>
    [DataField]
    public TimeSpan MinDurationForStrong { get; private set; } = TimeSpan.FromSeconds(60);
}
