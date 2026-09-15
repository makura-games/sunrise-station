using Content.Shared.Actions;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Components.Classes;
using Content.Shared.Metabolism;
using Content.Shared.DoAfter;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Polymorph;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.FixedPoint;

namespace Content.Shared._Sunrise.Antags.Vampires;

#region Basic Abilities

public sealed partial class VampireGlareActionEvent : InstantActionEvent
{
    /// <summary>
    /// Дистанция, на которой сущности ослепляются.
    /// </summary>
    [DataField]
    public float Range = 1f;

    /// <summary>Множитель эффекта взгляда, когда вампир слаб, а цель защищена от вспышек</summary>
    [DataField]
    public float FlashImmunityEffectScaleWeak = 0.0f;
    /// <summary>Множитель эффекта взгляда, когда вампир среднего уровня, а цель защищена от вспышек</summary>
    [DataField]
    public float FlashImmunityEffectScaleMid = 0.75f;

    /// <summary>
    /// Множитель эффекта при высоком уровне вампира
    /// </summary>
    [DataField]
    public float FlashImmunityEffectScaleStrong = 1f;

    /// <summary>
    /// Множитель эффекта при полной силе вампира
    /// </summary>
    [DataField]
    public float GlareEffectScaleFull = 1.5f;

    /// <summary>
    /// Сколько секунд парализуется цель перед источником взгляда.
    /// </summary>
    [DataField]
    public TimeSpan FrontParalyzeDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Сколько секунд парализуется цель позади источника взгляда.
    /// </summary>
    [DataField]
    public TimeSpan SideParalyzeDuration = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Урон по выносливости цели перед источником взгляда
    /// </summary>
    [DataField]
    public float FrontStaminaDamage = 25f;

    /// <summary>
    /// Урон по выносливости цели позади источника взгляда
    /// </summary>
    [DataField]
    public float BehindStaminaDamage = 25f;

    /// <summary>
    /// Урон по выносливости цели слева или справа от источника взгляда
    /// </summary>
    [DataField]
    public float SideStaminaDamage = 25f;

    /// <summary>
    /// Дополнительный урон по выносливости цели перед источником взгляда.
    /// </summary>
    [DataField]
    public float DotStaminaDamage = 5f;

    [DataField]
    public int DotTickCount = 10;

    [DataField]
    public TimeSpan DotTickInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// химикат и объём для введения целям.
    /// </summary>
    [DataField]
    public Dictionary<string, FixedPoint2> Reagents = new Dictionary<string, FixedPoint2>{ {"MuteToxin", 0.5} };

    /// <summary>
    /// Минимальное скалярное произведение векторов направления вампира и цели для срабатывания передней части способности взгляда
    /// </summary>
    [DataField]
    public float DotForwardLimit = 0.7f;

    /// <summary>
    /// Максимальное скалярное произведение векторов направления вампира и цели для срабатывания задней части способности взгляда
    /// </summary>
    [DataField]
    public float DotBackwardLimit = -0.7f;
}

public sealed partial class VampireSleepActionEvent : EntityTargetActionEvent
{
    /// <summary>
    ///     Длительность канала в секундах до усыпления цели
    /// </summary>
    [DataField]
    public TimeSpan ChannelTime = TimeSpan.FromSeconds(5);
    [DataField]
    public float SleepDistanceThreshold = 2.5f; //Как далеко может быть цель, чтобы сон сработал
    [DataField]
    public float SleepMovementThreshold = 0.1f; //Как далеко цель может отойти, чтобы сон сработал во время дуафтера
}

[Serializable, NetSerializable]
public sealed partial class VampireSleepDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public int BloodCost = 15;
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

}

public sealed partial class VampireRejuvenateIActionEvent : InstantActionEvent
{
    [DataField]
    public bool ResetStamina = true;

    [DataField]
    public bool RemoveStuns = true;
}

public sealed partial class VampireRejuvenateIIActionEvent : InstantActionEvent
{
    [DataField]
    public bool ResetStamina = true;

    [DataField]
    public bool RemoveStuns = true;

    [DataField]
    public FixedPoint2 ReagentPurgeAmount = FixedPoint2.New(10);

    [DataField]
    public HashSet<ProtoId<MetabolismStagePrototype>> PurgedMetabolismStages = new()
    {
        "Bloodstream",
    };

    [DataField]
    public int HealTicks = 5;

    [DataField]
    public TimeSpan HealTickInterval = TimeSpan.FromSeconds(3.5);

    [DataField]
    public Dictionary<string, FixedPoint2> HealGroups = new()
    {
        { "Brute", FixedPoint2.New(4) },
        { "Burn", FixedPoint2.New(4) },
    };

    [DataField]
    public Dictionary<string, FixedPoint2> HealTypes = new()
    {
        { "Poison", FixedPoint2.New(4) },
        { "Asphyxiation", FixedPoint2.New(5) },
    };
}

public sealed partial class VampireClassSelectActionEvent : InstantActionEvent;

public sealed partial class VampireToggleFangsActionEvent : InstantActionEvent;
public sealed partial class VampireLocateMindActionEvent : InstantActionEvent;

public sealed class VampireBloodDrankEvent : EntityEventArgs
{
    public EntityUid Target { get; }
    public float Amount { get; }

    public VampireBloodDrankEvent(EntityUid target, float amount)
    {
        Target = target;
        Amount = amount;
    }
}

public sealed class VampireFullPowerAchievedEvent : EntityEventArgs
{
}

/// <summary>
/// Локально вызывается у вампира при изменении связанных с прогрессией значений крови
/// </summary>
public sealed class VampireProgressionChangedEvent : EntityEventArgs { }

#endregion

[ByRefEvent]
public record struct VampireActionUseAttemptEvent(EntityUid User, EntityUid? ActionEntity = null, int BloodCost = 0, bool ShowPopup = true)
{
    public bool Allowed;
}

#region Hemomancer

// Вампирские когти
public sealed partial class VampireHemomancerClawsActionEvent : InstantActionEvent;

[ByRefEvent]
public readonly record struct VampireHemomancerClawsActivatedEvent(EntityUid Performer);

// Кровавая лоза
public sealed partial class VampireHemomancerTendrilsActionEvent : WorldTargetActionEvent
{
    [DataField]
    public EntProtoId TendrilsVisualPrototype = "VampireBloodTendrilVisual";

    [DataField]
    public EntProtoId TendrilsPuddlePrototype = "PuddleBlood";

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);

    [DataField]
    public float SlowMultiplier = 0.3f;

    [DataField]
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public FixedPoint2 ToxinDamage = FixedPoint2.New(33);

    [DataField]
    public bool SpawnVisuals = true;

    [DataField]
    public float PositionOffset = 0.5f;

    [DataField]
    public float TargetRange = 0.9f;

    [DataField]
    public TimeSpan VisualSpawnDelay = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan MinDelay = TimeSpan.Zero;

    [DataField]
    public TimeSpan MinSlowDuration = TimeSpan.FromSeconds(0.1);

    [DataField]
    public float MinSlowMultiplier = 0.05f;
}

// Кровавый барьер
public sealed partial class VampireBloodBarrierActionEvent : WorldTargetActionEvent
{
    [DataField]
    public EntProtoId BarrierPrototype = "VampireBloodBarrier";

    [DataField]
    public int BarrierCount = 3;
}

// Кровавая лужа
public sealed partial class VampireSanguinePoolActionEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId EnterEffectPrototype = "VampireSanguinePoolOut";

    [DataField]
    public EntProtoId ExitEffectPrototype = "VampireSanguinePoolIn";

    [DataField]
    public ProtoId<PolymorphPrototype> PolymorphPrototype = "VampireSanguinePoolPolymorph";

    [DataField]
    public SoundSpecifier EnterSound = new SoundPathSpecifier("/Audio/_Sunrise/Effects/vampire/enter_blood.ogg");

    [DataField]
    public SoundSpecifier ExitSound = new SoundPathSpecifier("/Audio/_Sunrise/Effects/vampire/exit_blood.ogg");

    [DataField]
    public TimeSpan BloodDripInterval = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(8);
}

// Кровавое извержение
public sealed partial class VampireBloodEruptionActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_Sunrise/Effects/vampire/blooderruption.ogg");

    [DataField]
    public float Range = 10f;

    [DataField]
    public FixedPoint2 Damage = FixedPoint2.New(50);

    [DataField]
    public float TargetRange = 2f;

    [DataField]
    public string PuddleReagent = "Blood";
}

// Обряд кровеносца
public sealed partial class VampireBloodBringersRiteActionEvent : InstantActionEvent
{
    [DataField]
    public float Range = 4f;

    [DataField]
    public FixedPoint2 Damage = FixedPoint2.New(5);

    [DataField]
    public float MaxTargetBlood = 10f;

    [DataField]
    public FixedPoint2 HealBrute = FixedPoint2.New(8);

    [DataField]
    public FixedPoint2 HealBurn = FixedPoint2.New(2);

    [DataField]
    public float HealStamina = 15f;

    [DataField]
    public TimeSpan ToggleInterval = TimeSpan.FromSeconds(2);

    [DataField]
    public int Cost = 10;

    [DataField]
    public int MaxTicks = 150;

    [DataField(required: true)]
    public EntProtoId BeamPrototype;
}

#endregion

#region Umbrae

// Плащ тьмы
public sealed partial class VampireCloakOfDarknessActionEvent : InstantActionEvent;

// Теневая ловушка
public sealed partial class VampireShadowSnareActionEvent : WorldTargetActionEvent
{
    [DataField]
    public EntProtoId SnarePrototype = "VampireShadowSnare";
}

// Якорь души
public sealed partial class VampireShadowAnchorActionEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId BeaconPrototype = "VampireShadowAnchorBeacon";

    /// <summary>
    /// Длительность doafter для установки якоря.
    /// </summary>
    [DataField]
    public TimeSpan PlaceDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Окно времени для возврата к якорю. После него возврат срабатывает автоматически.
    /// </summary>
    [DataField]
    public TimeSpan AutoReturnDelay = TimeSpan.FromMinutes(2);
}

[Serializable, NetSerializable]
public sealed partial class VampireShadowAnchorDoAfterEvent : SimpleDoAfterEvent
{
    [DataField("coordinates", required: true)]
    public NetCoordinates TargetCoordinates;

    [DataField]
    public EntProtoId BeaconPrototype = "VampireShadowAnchorBeacon";

    [DataField]
    public int BloodCost;

    [DataField]
    public TimeSpan AutoReturnDelay;

    private VampireShadowAnchorDoAfterEvent()
    {
    }

    public VampireShadowAnchorDoAfterEvent(NetCoordinates coords, EntProtoId beaconPrototype, int bloodCost, TimeSpan autoReturnDelay)
    {
        TargetCoordinates = coords;
        BeaconPrototype = beaconPrototype;
        BloodCost = bloodCost;
        AutoReturnDelay = autoReturnDelay;
    }

    public override DoAfterEvent Clone() => this;
}

// Тёмный проход
public sealed partial class VampireDarkPassageActionEvent : WorldTargetActionEvent
{
    [DataField]
    public EntProtoId MistInPrototype = "VampireDarkPassageMistIn";

    [DataField]
    public EntProtoId MistOutPrototype = "VampireDarkPassageMistOut";
    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");
}

// Гашение
public sealed partial class VampireExtinguishActionEvent : InstantActionEvent
{
    [DataField]
    public float Radius = 4f;
}

// Теневой бокс
public sealed partial class VampireShadowBoxingActionEvent : EntityTargetActionEvent
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(0.9);

    [DataField]
    public int BrutePerTick = 6;

    [DataField]
    public float Range = 4f;

    [DataField]
    public SoundSpecifier? HitSound;

    [DataField]
    public EntProtoId PunchEffectPrototype = "WeaponArcPunch";
}

[ByRefEvent]
public record struct VampireShadowBoxingStartAttemptEvent(EntityUid Performer, EntityUid Target)
{
    public bool Cancelled;
}

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class VampireShadowBoxingPunchEvent : EntityEventArgs
{
    public NetEntity Source { get; }
    public NetEntity Target { get; }

    public VampireShadowBoxingPunchEvent(NetEntity source, NetEntity target)
    {
        Source = source;
        Target = target;
    }
    [DataField]
    public TimeSpan PunchLifetime = TimeSpan.FromSeconds(0.33);
    [DataField]
    public string EffectProto = "VampireShadowBoxingPunch";
}

// Вечная тьма
public sealed partial class VampireEternalDarknessActionEvent : InstantActionEvent
{
    [DataField]
    public EntProtoId AuraPrototype = "VampireEternalDarknessAura";

    [DataField]
    public int MaxTicks = 360;

    [DataField]
    public int BloodPerTick = 5;

    [DataField]
    public float FreezeRadius = 6f;

    [DataField]
    public float TargetFreezeTemp = 233.15f;

    /// <summary>
    /// Интервал между снижениями температуры цели.
    /// </summary>
    [DataField]
    public TimeSpan TempDropInterval = TimeSpan.FromSeconds(2);

    [DataField]
    public float TempDropPerInterval = 60f;
}

#endregion

#region Dantalion

public sealed partial class VampireEnthrallActionEvent : EntityTargetActionEvent
{
    /// <summary>
    ///     Длительность канала в секундах до порабощения цели
    /// </summary>
    [DataField]
    public TimeSpan ChannelTime = TimeSpan.FromSeconds(15);
}

[Serializable, NetSerializable]
public sealed partial class VampireDrinkBloodDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class VampireDevourDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public float BloodFullnessRestore;
}

[Serializable, NetSerializable]
public sealed partial class VampireEnthrallDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public int BloodCost;
}

public sealed partial class VampirePacifyActionEvent : EntityTargetActionEvent
{
    [DataField]
    public TimeSpan PacifyDuration = TimeSpan.FromSeconds(40);
}

public sealed partial class VampireSubspaceSwapActionEvent : EntityTargetActionEvent
{
    [DataField]
    public TimeSpan SlowDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public float SlowMultiplier = 0.4f;
    [DataField]
    public TimeSpan HysteriaDuration = TimeSpan.FromSeconds(15);

    [DataField(required: true)]
    public List<HysteriaDisguiseSprite> HysteriaDisguiseSprites = new();
}

public sealed partial class VampireDecoyActionEvent : InstantActionEvent
{
    [DataField]
    public TimeSpan DecoyDuration = TimeSpan.FromSeconds(6);
    [DataField]
    public TimeSpan InvisibilityDuration = TimeSpan.FromSeconds(6);
    [DataField]
    public float DecoyVisibility = -1f;
    [DataField]
    public bool DecoyFlashDisplayPopup = true;
    [DataField]
    public float DecoyFlashProbability = 1f;
}

[ByRefEvent]
public record struct VampireDecoyActivatedEvent(
    Entity<DantalionComponent> Dantalion,
    VampireDecoyActionEvent Action,
    TimeSpan InvisibilityDuration,
    bool HadStealthComponent,
    bool PreviousStealthEnabled,
    float PreviousStealthVisibility);

public sealed partial class VampireRallyThrallsActionEvent : InstantActionEvent
{
    /// <summary>
    ///     Дальность поиска тхраллов в тайлах
    /// </summary>
    [DataField]
    public float Range = 7f;
}

public sealed partial class VampireBloodBondActionEvent : InstantActionEvent
{
    /// <summary>
    ///     Дальность связи кровью в тайлах
    /// </summary>
    [DataField]
    public float Range = 3f;

    /// <summary>
    ///     Стоимость крови за тик активности
    /// </summary>
    [DataField]
    public int BloodCostPerTick = 5;

    /// <summary>
    ///     Интервал тика
    /// </summary>
    [DataField]
    public TimeSpan TickInterval = TimeSpan.FromSeconds(2);

    [DataField(required: true)]
    public EntProtoId BeamPrototype;
}

[ByRefEvent]
public record struct VampireBloodBondStartAttemptEvent(Entity<DantalionComponent> Dantalion)
{
    public bool Cancelled;
}

[ByRefEvent]
public readonly record struct VampireBloodBondStartedEvent(Entity<DantalionComponent> Dantalion, VampireBloodBondActionEvent Action);

[ByRefEvent]
public readonly record struct VampireBloodBondStoppedEvent(Entity<DantalionComponent> Dantalion);

public sealed partial class VampireMassHysteriaActionEvent : InstantActionEvent
{
    /// <summary>
    ///     Дальность воздействия на цели в тайлах
    /// </summary>
    [DataField]
    public float Range = 8f;

    /// <summary>
    ///     Длительность вспышки в секундах
    /// </summary>
    [DataField]
    public TimeSpan FlashDuration = TimeSpan.FromSeconds(3);

    /// <summary>
    ///     Длительность эффекта истерического зрения в секундах
    /// </summary>
    [DataField]
    public TimeSpan HysteriaDuration = TimeSpan.FromSeconds(30);

    [DataField(required: true)]
    public List<HysteriaDisguiseSprite> HysteriaDisguiseSprites = new();

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_Sunrise/Effects/vampire/sound_hallucinations_im_here1.ogg");
}

#endregion

#region Gargantua

public sealed partial class VampireBloodSwellActionEvent : InstantActionEvent
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(30);
    /// <summary>
    ///     Общее количество крови, необходимое для бонуса к рукопашному урону.
    /// </summary>
    [DataField]
    public float EnhancedThreshold = 400f;

    /// <summary>
    ///     Бонусный тупой урон к рукопашным ударам при усилении.
    /// </summary>
    [DataField]
    public float MeleeBonusDamage = 14f;

    [DataField]
    public ProtoId<DamageTypePrototype> MeleeBonusDamageType = "Blunt";

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

    [DataField]
    public float IncomingDamageMultiplier = 0.5f;

    [DataField]
    public float StaminaDamageMultiplier = 0.5f;

    [DataField]
    public float StatusEffectDurationMultiplier = 0.5f;
}

public sealed partial class VampireBloodRushActionEvent : InstantActionEvent
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Множитель скорости движения, пока активен Кровавый рывок.
    /// </summary>
    [DataField]
    public float SpeedMultiplier = 1.5f;
}

public sealed partial class VampireSeismicStompActionEvent : InstantActionEvent
{
    /// <summary>
    ///     Радиус эффекта топота в тайлах
    /// </summary>
    [DataField]
    public float Radius = 3f;

    /// <summary>
    ///     Дистанция отбрасывания целей в тайлах
    /// </summary>
    [DataField]
    public float ThrowDistance = 3f;
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/Footsteps/largethud.ogg");

    /// <summary>
    /// Прототип визуального эффекта топота.
    /// </summary>
    [DataField]
    public EntProtoId EffectPrototype = "VampireSeismicStompEffect";
}

public sealed partial class VampireOverwhelmingForceActionEvent : InstantActionEvent;

public sealed partial class VampireDemonicGraspActionEvent : WorldTargetActionEvent
{
    /// <summary>
    ///     Максимальная дальность снаряда хватки
    /// </summary>
    [DataField]
    public float Range = 15f;

    /// <summary>
    ///     Длительность обездвиживания в секундах
    /// </summary>
    [DataField]
    public TimeSpan ImmobilizeDuration = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     Скорость снаряда хватки
    /// </summary>
    [DataField]
    public float ProjectileSpeed = 15f;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/_Sunrise/Effects/vampire/exit_blood.ogg");

    [DataField]
    public TimeSpan TileInterval = TimeSpan.FromMilliseconds(50);

    [DataField]
    public EntProtoId EffectPrototype = "VampireDemonicGraspEffect";

    [DataField]
    public EntProtoId ImmobilizedEffectPrototype = "VampireImmobilizedEffect";
}

public sealed partial class VampireChargeActionEvent : WorldTargetActionEvent
{
    /// <summary>
    ///     Тупой урон существам при столкновении
    /// </summary>
    [DataField]
    public float CreatureDamage = 60f;

    /// <summary>
    ///     Дистанция отбрасывания существ при столкновении
    /// </summary>
    [DataField]
    public float CreatureThrowDistance = 5f;

    /// <summary>
    ///     Структурный урон строениям/механизмам
    /// </summary>
    [DataField]
    public float StructuralDamage = 150f;

    /// <summary>
    ///     Скорость движения рывка
    /// </summary>
    [DataField]
    public float ChargeSpeed = 35f;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/Footsteps/largethud.ogg");
}

#endregion
