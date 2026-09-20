using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Antags.Vampires.Components;

/// <summary>
/// Святая вода и святые места: урон и таймеры для вампира.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentPause]
public sealed partial class VampireHolyComponent : Component
{
    /// <summary>
    /// Радиус, в котором вокруг вампира обнаруживаются святые места.
    /// </summary>
    [DataField]
    public float HolyPlaceRange = 8f;

    /// <summary>
    /// Задержка между тиками урона от святой воды/места.
    /// </summary>
    [DataField]
    public TimeSpan HolyTickDelay = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Реагент, наносящий вампиру урон как святая вода.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype> HolyWaterReagentId = "Holywater";

    /// <summary>
    /// Таймер кулдауна тиков урона святой воды.
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextHolyWaterTick = TimeSpan.Zero;

    /// <summary>
    /// Таймер кулдауна тиков урона святых мест.
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextHolyPlaceTick = TimeSpan.Zero;

    /// <summary>
    /// Таймер кулдауна всплывающих уведомлений святых мест.
    /// </summary>
    [AutoPausedField]
    public TimeSpan NextHolyPlacePopup = TimeSpan.Zero;
}
