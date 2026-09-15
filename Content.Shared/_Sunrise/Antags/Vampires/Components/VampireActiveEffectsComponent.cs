using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using System.Numerics;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireRejuvenateComponent : Component
{
    /// <summary>
    /// Оставшееся количество тиков лечения.
    /// </summary>
    [DataField] public int TicksRemaining;

    /// <summary>
    /// Интервал между тиками лечения.
    /// </summary>
    [DataField] public TimeSpan TickInterval = TimeSpan.FromSeconds(3.5);

    /// <summary>
    /// Время следующего тика лечения.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan NextTick;

    /// <summary>
    /// Группы урона, восстанавливаемые за тик.
    /// </summary>
    [DataField] public Dictionary<string, FixedPoint2> HealGroups = new();

    /// <summary>
    /// Типы урона, восстанавливаемые за тик.
    /// </summary>
    [DataField] public Dictionary<string, FixedPoint2> HealTypes = new();
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireGlareDotComponent : Component
{
    /// <summary>
    /// Источник эффекта (вампир, применивший Взгляд).
    /// </summary>
    [DataField] public EntityUid Source;

    /// <summary>
    /// Урон по выносливости за тик.
    /// </summary>
    [DataField] public float StaminaDamage;

    /// <summary>
    /// Оставшееся количество тиков урона.
    /// </summary>
    [DataField] public int TicksRemaining;

    /// <summary>
    /// Интервал между тиками урона.
    /// </summary>
    [DataField] public TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Время следующего тика урона.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan NextTick;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampirePacifyComponent : Component
{
    /// <summary>
    /// Время окончания эффекта Умиротворения.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan EndTime;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireInvisibilityComponent : Component
{
    /// <summary>
    /// Время окончания невидимости.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan EndTime;

    /// <summary>
    /// Был ли StealthComponent у сущности до активации невидимости.
    /// </summary>
    [DataField] public bool HadStealthComponent;

    /// <summary>
    /// Предыдущее состояние включённости StealthComponent.
    /// </summary>
    [DataField] public bool PreviousStealthEnabled;

    /// <summary>
    /// Предыдущее значение видимости StealthComponent.
    /// </summary>
    [DataField] public float PreviousStealthVisibility = 1f;
}

[RegisterComponent]
public sealed partial class ActiveVampireHemomancerClawsComponent : Component
{
    /// <summary>
    /// Сущность созданных Кровавых когтей.
    /// </summary>
    [DataField] public EntityUid? SpawnedClaws;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireBloodBondComponent : Component
{
    /// <summary>
    /// Сущность действия Кровавой связи.
    /// </summary>
    [DataField] public EntityUid ActionEntity;

    /// <summary>
    /// Радиус действия Кровавой связи.
    /// </summary>
    [DataField] public float Range;

    /// <summary>
    /// Стоимость крови за тик активной Кровавой связи.
    /// </summary>
    [DataField] public int BloodCostPerTick;

    /// <summary>
    /// Интервал между тиками Кровавой связи.
    /// </summary>
    [DataField] public TimeSpan TickInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Время следующего тика Кровавой связи.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan NextTick;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireBloodBringersRiteComponent : Component
{
    /// <summary>
    /// Оставшееся количество тиков Обряда.
    /// </summary>
    [DataField] public int TicksRemaining = 150;

    /// <summary>
    /// Стоимость крови за тик Обряда.
    /// </summary>
    [DataField] public int BloodCost;

    /// <summary>
    /// Радиус действия лучей Обряда.
    /// </summary>
    [DataField] public float Range;

    /// <summary>
    /// Урон, наносимый целям за тик.
    /// </summary>
    [DataField] public FixedPoint2 Damage;

    /// <summary>
    /// Тупой урон, лечащийся у вампира за тик.
    /// </summary>
    [DataField] public FixedPoint2 HealBrute;

    /// <summary>
    /// Ожоговый урон, лечащийся у вампира за тик.
    /// </summary>
    [DataField] public FixedPoint2 HealBurn;

    /// <summary>
    /// Восстанавливаемая выносливость за тик.
    /// </summary>
    [DataField] public float HealStamina;

    /// <summary>
    /// Интервал между тиками Обряда.
    /// </summary>
    [DataField] public TimeSpan TickInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Прототип визуального луча вытягивания.
    /// </summary>
    [DataField] public EntProtoId BeamPrototype;

    /// <summary>
    /// Время следующего тика Обряда.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan NextTick;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireEternalDarknessComponent : Component
{
    /// <summary>
    /// Оставшееся количество тиков Вечной тьмы.
    /// </summary>
    [DataField] public int TicksRemaining;

    /// <summary>
    /// Стоимость крови за тик Вечной тьмы.
    /// </summary>
    [DataField] public int BloodPerTick;

    /// <summary>
    /// Время следующего снижения температуры.
    /// </summary>
    [DataField]
    public TimeSpan NextTempDropTime;

    /// <summary>
    /// Интервал между снижениями температуры.
    /// </summary>
    [DataField]
    public TimeSpan TempDropInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Радиус заморозки вокруг ауры.
    /// </summary>
    [DataField] public float FreezeRadius;

    /// <summary>
    /// Целевая температура заморозки.
    /// </summary>
    [DataField] public float TargetFreezeTemp;

    /// <summary>
    /// Снижение температуры за интервал.
    /// </summary>
    [DataField] public float TempDropPerInterval;

    /// <summary>
    /// Время следующего тика Вечной тьмы.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan NextTick;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireShadowBoxingComponent : Component
{
    /// <summary>
    /// Цель Теневого бокса.
    /// </summary>
    [DataField] public EntityUid Target;

    /// <summary>
    /// Радиус, в котором наносится удар.
    /// </summary>
    [DataField] public float Range;

    /// <summary>
    /// Тупой урон за удар.
    /// </summary>
    [DataField] public int BrutePerTick;

    /// <summary>
    /// Звук удара.
    /// </summary>
    [DataField] public SoundSpecifier? HitSound;

    /// <summary>
    /// Прототип визуального эффекта пролёта удара.
    /// </summary>
    [DataField] public EntProtoId PunchEffectPrototype = "WeaponArcPunch";

    /// <summary>
    /// Интервал между ударами.
    /// </summary>
    [DataField] public TimeSpan TickInterval = TimeSpan.FromSeconds(0.9);

    /// <summary>
    /// Время следующего удара.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan NextTick;

    /// <summary>
    /// Время окончания Теневого бокса.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan EndTime;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PendingVampireTendrilsComponent : Component
{
    /// <summary>
    /// Координаты тайла, на котором появятся Кровавые щупальца.
    /// </summary>
    [DataField] public EntityCoordinates TileCoordinates;

    /// <summary>
    /// Прототип лужи крови, создаваемой щупальцами.
    /// </summary>
    [DataField] public EntProtoId PuddlePrototype = "PuddleBlood";

    /// <summary>
    /// Радиус захвата целей щупальцами.
    /// </summary>
    [DataField] public float TargetRange;

    /// <summary>
    /// Длительность замедления целей щупальцами.
    /// </summary>
    [DataField] public TimeSpan SlowDuration;

    /// <summary>
    /// Множитель замедления целей щупальцами.
    /// </summary>
    [DataField] public float SlowMultiplier;

    /// <summary>
    /// Токсичный урон, наносимый целям щупальцами.
    /// </summary>
    [DataField] public FixedPoint2 ToxinDamage;

    /// <summary>
    /// Время срабатывания щупалец.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan TriggerTime;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireDemonicGraspComponent : Component
{
    /// <summary>
    /// Начальные координаты Демонической хватки.
    /// </summary>
    [DataField] public EntityCoordinates StartCoordinates;

    /// <summary>
    /// Сетка, по которой распространяется хватка.
    /// </summary>
    [DataField] public EntityUid GridUid;

    /// <summary>
    /// Направление распространения хватки.
    /// </summary>
    [DataField] public Vector2 Direction;

    /// <summary>
    /// Текущий пройденный тайл.
    /// </summary>
    [DataField] public int CurrentTile;

    /// <summary>
    /// Максимальное количество пройденных тайлов.
    /// </summary>
    [DataField] public int MaxTiles;

    /// <summary>
    /// Интервал перехода на следующий тайл.
    /// </summary>
    [DataField] public TimeSpan TileInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Длительность обездвиживания цели.
    /// </summary>
    [DataField] public TimeSpan ImmobilizeDuration;

    /// <summary>
    /// Притягивать ли цель к вампиру.
    /// </summary>
    [DataField] public bool PullTarget;

    /// <summary>
    /// Прототип визуального эффекта хватки.
    /// </summary>
    [DataField] public EntProtoId EffectPrototype = "VampireDemonicGraspEffect";

    /// <summary>
    /// Прототип эффекта обездвиживания.
    /// </summary>
    [DataField] public EntProtoId ImmobilizedEffectPrototype = "VampireImmobilizedEffect";

    /// <summary>
    /// Время перехода на следующий тайл.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))] [AutoPausedField]
    public TimeSpan NextTileTime;
}
