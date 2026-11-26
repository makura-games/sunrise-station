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

    [AutoNetworkedField]
    public int ApplicationsRemaining;

    [AutoNetworkedField]
    public TimeSpan ApplicationInterval = TimeSpan.FromSeconds(3.5);

    [AutoNetworkedField, AutoPausedField]
    public TimeSpan NextApplication;

    /// <summary>
    /// Изменение урона за шаг; отрицательные значения лечат.
    /// </summary>
    [AutoNetworkedField]
    public DamageSpecifier Healing = new();
}
