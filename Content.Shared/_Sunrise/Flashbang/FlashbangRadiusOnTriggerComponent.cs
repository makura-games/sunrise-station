using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Flashbang;

/// <summary>
/// Применяет радиальный оглушающий эффект вспышки при срабатывании триггера.
/// Сила эффекта линейно убывает с расстоянием от источника.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlashbangRadiusOnTriggerComponent : BaseXOnTriggerComponent
{
    /// <summary>
    /// Радиус действия в тайлах.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Range = 7f;

    /// <summary>
    /// Базовая длительность стана на нулевой дистанции.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan StunDuration = TimeSpan.FromSeconds(4f);

    /// <summary>
    /// Базовая длительность падения на нулевой дистанции.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan KnockdownDuration = TimeSpan.FromSeconds(4f);

    /// <summary>
    /// Минимальный коэффициент силы (0..1). Если t = 1 − effectiveDist/Range меньше этого
    /// значения, эффект не применяется. Отсекает незначительный стан на краях зоны.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinEffectStrength = 0.1f;

    /// <summary>
    /// Если true — защита экипировки всех целей игнорируется.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool IgnoreResistances;

    /// <summary>
    /// Минимальное давление атмосферы в источнике (кПа), при котором эффект применяется.
    /// В вакууме и разреженной среде звук не распространяется.
    /// </summary>
    [DataField]
    public float MinAmbientPressure = 5f;
}
