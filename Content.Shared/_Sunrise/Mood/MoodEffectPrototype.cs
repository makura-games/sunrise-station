using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Mood;

[Prototype]
public sealed partial class MoodEffectPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    public string Description => Loc.GetString($"mood-effect-{ID}");

    [DataField]
    public ProtoId<MoodCategoryPrototype>? Category;

    [DataField(required: true)]
    public float MoodChange;

    [DataField]
    public int Timeout;

    [DataField]
    public bool Hidden;
}
