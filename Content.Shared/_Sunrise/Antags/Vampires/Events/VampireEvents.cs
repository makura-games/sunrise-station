using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Antags.Vampires.Events;

/// <summary>
/// Событие вампирского взгляда.
/// </summary>
public sealed partial class VampireGlareActionEvent : InstantActionEvent;

/// <summary>
/// Событие гипноза.
/// </summary>
public sealed partial class VampireSleepActionEvent : EntityTargetActionEvent;

/// <summary>
/// Данные гипноза.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class VampireSleepDoAfterEvent : SimpleDoAfterEvent
{
    /// <summary>
    /// Цель.
    /// </summary>
    [DataField(required: true)]
    public NetEntity Victim;

    /// <summary>
    /// Action для возврата заряда.
    /// </summary>
    [DataField(required: true)]
    public NetEntity Action;

    /// <summary>
    /// Дистанция прерывания.
    /// </summary>
    [DataField]
    public float MaxDistance = 2.5f;

    /// <summary>
    /// Стоимость в крови.
    /// </summary>
    [DataField]
    public int BloodCost = 20;

    /// <summary>
    /// Длительность сна.
    /// </summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Игнорирование веры.
    /// </summary>
    [DataField]
    public bool IgnoresFaith;
}

/// <summary>
/// Событие глотка крови.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class VampireDrinkBloodDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Событие омоложения.
/// </summary>
public sealed partial class VampireRejuvenateIActionEvent : InstantActionEvent;

/// <summary>
/// Событие высшего омоложения.
/// </summary>
public sealed partial class VampireRejuvenateIiActionEvent : InstantActionEvent;

/// <summary>
/// Событие переключения клыков.
/// </summary>
public sealed partial class VampireToggleFangsActionEvent : InstantActionEvent;
