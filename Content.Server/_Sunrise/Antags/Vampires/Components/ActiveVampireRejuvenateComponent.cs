using Content.Shared.Damage;

namespace Content.Server._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Активное омоложение.
/// </summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class ActiveVampireRejuvenateComponent : Component
{
    /// <summary>
    /// Осталось применений.
    /// </summary>
    public int ApplicationsRemaining;

    /// <summary>
    /// Интервал лечения.
    /// </summary>
    public TimeSpan ApplicationInterval = TimeSpan.FromSeconds(3.5);

    /// <summary>
    /// Следующее лечение.
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextApplication;

    /// <summary>
    /// Лечение за применение.
    /// </summary>
    public DamageSpecifier Healing = new();
}
