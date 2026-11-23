using Content.Shared.Destructible.Thresholds;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Presets;

[Prototype]
public sealed partial class GamePresetPoolPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     Presets with their respective player limits.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<string, MinMax> Presets { get; private set; } = new();
}
