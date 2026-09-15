using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Antags.Vampires.Components.Classes;

/// <summary>
/// Маркерный компонент активного Кровавого рывка
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ActiveBloodRushComponent : Component
{
    /// <summary>
    /// Время окончания действия Кровавого рывка.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan EndTime;

    /// <summary>
    /// Множитель скорости движения во время Кровавого рывка.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpeedMultiplier = 1.5f;
}
