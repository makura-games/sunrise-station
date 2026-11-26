using Robust.Shared.Audio;

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
    public string Layer = "sunrisePoweredLightFlicker";

    /// <summary>
    /// Карта слоя спрайта с искрами.
    /// </summary>
    [DataField]
    public string SparksLayer = "sunrisePoweredLightSparks";

    /// <summary>
    /// Состояния мерцания, доступные для сломанного светильника.
    /// </summary>
    [DataField(required: true)]
    public List<string> States = [];

    /// <summary>
    /// Состояния искр, проигрываемые одновременно с мерцанием.
    /// </summary>
    [DataField]
    public List<string> SparkStates = [];

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
    /// Решение сервера о мерцании текущей поломки.
    /// </summary>
    public bool? ShouldFlicker;

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
    /// Нагрузка разбитой включённой лампы, необходимая для определения наличия питания.
    /// </summary>
    public const float BrokenPowerLoad = 1f;
}
