namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// Общий класс возбудителя болезни. Нужен диагностике и лечению, бьющему по всему классу,
/// но не для ветвления логики болезней в коде.
/// </summary>
public enum DiseaseClassification : byte
{
    Viral,
    Bacterial,
    Fungal,
    Parasitic,
    Environmental,
    Anomalous,
}
