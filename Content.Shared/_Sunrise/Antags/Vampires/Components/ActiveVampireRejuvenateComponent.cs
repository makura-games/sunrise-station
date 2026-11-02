using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Периодическое лечение улучшенного омоложения.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
public sealed partial class ActiveVampireRejuvenateComponent : Component
{
    public override bool SendOnlyToOwner => true;

    /// <summary>
    /// Оставшееся число шагов лечения.
    /// </summary>
    [AutoNetworkedField]
    public int ApplicationsRemaining;

    /// <summary>
    /// Интервал лечения.
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan ApplicationInterval = TimeSpan.FromSeconds(3.5);

    /// <summary>
    /// Время следующего шага лечения.
    /// </summary>
    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextApplication;

    /// <summary>
    /// Изменение урона за шаг; отрицательные значения лечат.
    /// </summary>
    [AutoNetworkedField]
    public DamageSpecifier Healing = new();
}
