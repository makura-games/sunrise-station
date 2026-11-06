using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Whitelist;

namespace Content.Shared._Sunrise.Boss.Components;

/// <summary>
/// Компонент, обозначающий, что сущность будет пытаться продамажить тех, кто с ней соприкасается
/// </summary>
[RegisterComponent]
public sealed partial class DamageOnCollideComponent : Component
{
    [DataField("damageOnCollide")]
    public DamageSpecifier Damage = new DamageSpecifier()
    {
        DamageDict = new()
        {
            { "Slash", 10 },
            { "Piercing", 10 },
        }
    };

    [DataField]
    public EntityWhitelist? Blacklist;
}
