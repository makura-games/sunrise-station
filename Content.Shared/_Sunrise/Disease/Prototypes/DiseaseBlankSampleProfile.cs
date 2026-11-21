namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// Уровень параметров заготовки; название и внешний вид пробирки от него не зависят.
/// </summary>
public enum DiseaseBlankSampleQuality : byte
{
    Weak,
    Moderate,
    Strong,
}

/// <summary>
/// Вес выбора и диапазоны начальных параметров заготовки одного уровня.
/// </summary>
[DataDefinition]
public sealed partial class DiseaseBlankSampleProfile
{
    /// <summary>Относительный вес выбора профиля.</summary>
    [DataField]
    public float Weight = 1f;

    /// <summary>Минимальный размер пула симптомов.</summary>
    [DataField]
    public int MinSymptoms = 7;

    /// <summary>Максимальный размер пула симптомов включительно.</summary>
    [DataField]
    public int MaxSymptoms = 9;

    /// <summary>Минимальное число доступных стадий.</summary>
    [DataField]
    public int MinMaxStage = 4;

    /// <summary>Максимальное число доступных стадий включительно.</summary>
    [DataField]
    public int MaxMaxStage = 5;

    /// <summary>Нижняя граница начальной устойчивости.</summary>
    [DataField]
    public float MinResistance = 0.15f;

    /// <summary>Верхняя граница начальной устойчивости.</summary>
    [DataField]
    public float MaxResistance = 0.3f;

    /// <summary>
    /// Шанс, что на последней стадии окажется полезный симптом.
    /// </summary>
    [DataField]
    public float BeneficialChance;
}
