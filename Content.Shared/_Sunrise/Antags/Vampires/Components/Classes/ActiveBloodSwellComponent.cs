using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;

namespace Content.Shared._Sunrise.Antags.Vampires.Components.Classes;

/// <summary>
/// Маркерный компонент активного Кровавого вспучивания
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ActiveBloodSwellComponent : Component
{
    /// <summary>
    /// Время окончания действия Кровавого вспучивания.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;

    /// <summary>
    /// Порог TotalBlood для усиленного режима Кровавого вспучивания.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float EnhancedThreshold = 400f;

    /// <summary>
    /// Бонусный урон в ближнем бою при активном Кровавом вспучивании.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 MeleeBonusDamage = FixedPoint2.New(14f);

    /// <summary>
    /// Тип бонусного урона в ближнем бою.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<DamageTypePrototype> MeleeBonusDamageType = "Blunt";

    /// <summary>
    /// Типы входящего урона, умножаемые на IncomingDamageMultiplier.
    /// </summary>
    [DataField]
    public HashSet<string> ReducedDamageTypes = new()
    {
        "Blunt",
        "Slash",
        "Piercing",
        "Heat",
        "Cold",
        "Shock",
        "Caustic",
    };

    /// <summary>
    /// Множитель входящего урона перечисленных типов.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float IncomingDamageMultiplier = 0.5f;

    /// <summary>
    /// Множитель входящего урона по выносливости.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StaminaDamageMultiplier = 0.5f;

    /// <summary>
    /// Множитель длительности оглушений/нокдаунов/замедлений при активном эффекте.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StatusEffectDurationMultiplier = 0.5f;
}
