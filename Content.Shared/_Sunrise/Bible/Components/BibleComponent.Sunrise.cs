using Content.Shared.Damage;

namespace Content.Shared.Bible.Components;

public sealed partial class BibleComponent
{
    /// <summary>
    /// Damage dealt to an unholy user when they use the bible.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier DamageOnUnholyUse = default!;

    /// <summary>
    /// Additional damage dealt to an unholy target.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier DamageUnholy = default!;
}
