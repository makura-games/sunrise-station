using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Antags.Vampires.Events;

public sealed partial class VampireGlareActionEvent : InstantActionEvent;

public sealed partial class VampireSleepActionEvent : EntityTargetActionEvent;

/// <summary>
/// Параметры усыпления на момент начала DoAfter.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class VampireSleepDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => (VampireSleepDoAfterEvent)MemberwiseClone();

    [DataField(required: true)]
    public NetEntity Victim;

    /// <summary>
    /// Действие для возврата заряда.
    /// </summary>
    [DataField(required: true)]
    public NetEntity Action;

    /// <summary>
    /// Предельная дистанция доступности цели во время концентрации.
    /// </summary>
    [DataField]
    public float MaxDistance = 2.5f;

    /// <summary>
    /// Стоимость в DrunkBlood после успешной концентрации.
    /// </summary>
    [DataField]
    public int BloodCost = 20;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Обход только защиты веры.
    /// </summary>
    [DataField]
    public bool IgnoresFaith;
}

[Serializable, NetSerializable]
public sealed partial class VampireDrinkBloodDoAfterEvent : SimpleDoAfterEvent;

public sealed partial class VampireRejuvenateActionEvent : InstantActionEvent;

public sealed partial class VampireToggleFangsActionEvent : InstantActionEvent;

/// <summary>
/// Вызывается после повышения уровня силы вампира; сервер отправляет соответствующее сообщение.
/// </summary>
[ByRefEvent]
public readonly record struct VampirePowerLevelChangedEvent(VampirePowerLevel Level);

/// <summary>
/// Общая инициализация вампира завершена; сервер может настроить метаболизм и уязвимости.
/// </summary>
[ByRefEvent]
public readonly record struct VampireStartupEvent;

/// <summary>
/// Вампир отключается; сервер освобождает своё состояние до удаления общих компонентов.
/// </summary>
[ByRefEvent]
public readonly record struct VampireShutdownEvent;
