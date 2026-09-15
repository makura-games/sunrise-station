using Robust.Shared.Audio;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.Light.Visualizers;

/// <summary>
/// Показывает объединенные мерцание и искры на поврежденном светильнике.
/// </summary>
[RegisterComponent]
public sealed partial class SunrisePoweredLightSparksComponent : Component
{
    /// <summary>
    /// Карта слоя спрайта, на котором отображается мерцание.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public string Layer = "sunrisePoweredLightFlicker";

    /// <summary>
    /// Карта слоя спрайта с искрами.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public string SparksLayer = "sunrisePoweredLightSparks";

    /// <summary>
    /// Состояния мерцания, доступные для сломанного светильника.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public List<string> States = [];

    /// <summary>
    /// Состояния искр, проигрываемые одновременно с мерцанием.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public List<string> SparkStates = [];

    /// <summary>
    /// Путь к RSI с искрами для резервного слоя.
    /// </summary>
    [DataField]
    [ViewVariables(VVAccess.ReadOnly)]
    public ResPath? SparkSprite;

    /// <summary>
    /// Минимальная пауза между вспышками поврежденного светильника.
    /// </summary>
    [DataField]
    public TimeSpan MinFlickerDelay = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Максимальная пауза между вспышками поврежденного светильника.
    /// </summary>
    [DataField]
    public TimeSpan MaxFlickerDelay = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Длительность одной вспышки.
    /// </summary>
    [DataField]
    public TimeSpan FlickerDuration = TimeSpan.FromSeconds(0.6);

    /// <summary>
    /// Вероятность того, что разбитая лампа будет мерцать, а не останется полностью выключенной.
    /// </summary>
    [DataField]
    public float FlickerChance = 0.65f;

    /// <summary>
    /// Вероятность появления искр во время отдельной вспышки.
    /// </summary>
    [DataField]
    public float SparksChance = 0.5f;

    /// <summary>
    /// Доля обычной энергии лампы во время вспышки.
    /// </summary>
    [DataField]
    public float FlickerLightEnergyMultiplier = 0.15f;

    /// <summary>
    /// Гул, воспроизводимый во время мерцания повреждённого светильника.
    /// </summary>
    [DataField]
    public SoundSpecifier? FlickerSound;

    /// <summary>
    /// Последняя обработанная клиентом последовательность вспышки.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public int FlickerSequence;

    /// <summary>
    /// Выбранное сервером поведение текущей разбитой лампы.
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool? ShouldFlicker;
}
