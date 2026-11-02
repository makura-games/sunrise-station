using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Активное омоложение.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), AutoGenerateComponentPause]
public sealed partial class ActiveVampireRejuvenateComponent : Component
{
    public override bool SendOnlyToOwner => true;

    /// <summary>
    /// Осталось применений.
    /// </summary>
    [AutoNetworkedField]
    public int ApplicationsRemaining;

    /// <summary>
    /// Интервал лечения.
    /// </summary>
    [AutoNetworkedField]
    public TimeSpan ApplicationInterval = TimeSpan.FromSeconds(3.5);

    /// <summary>
    /// Следующее лечение.
    /// </summary>
    [AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan NextApplication;

    /// <summary>
    /// Лечение за применение.
    /// </summary>
    [AutoNetworkedField]
    public DamageSpecifier Healing = new();
}
