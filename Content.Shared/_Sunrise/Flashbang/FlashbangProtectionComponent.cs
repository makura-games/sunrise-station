using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Flashbang;

/// <summary>
/// Компонент на предметах экипировки (шлем, наушники), обеспечивающий защиту
/// от радиального оглушающего эффекта вспышки.
/// Защита выражается в виде виртуального дополнительного расстояния от источника.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlashbangProtectionComponent : Component
{
    /// <summary>
    /// Какая доля от радиуса текущей вспышки будет добавлена как виртуальная дистанция.
    /// Значение 0.5 уменьшает максимальный эффект и эффективный радиус вдвое.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ProtectionRangeCoefficient = 0.5f;
}
