namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// Выброс капель, привязанный к симптому.
/// </summary>
[DataDefinition]
public sealed partial class DiseaseSpread
{
    /// <summary>
    /// Радиус, в котором капли достают до существ. Стены, окна и шлюзы перекрывают выброс.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// Базовый шанс заражения для каждого попавшего под выброс, до поправок на стадию и защиту.
    /// </summary>
    [DataField]
    public float Chance = 0.2f;
}
