namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// Общий класс возбудителя болезни. Нужен для диагностировщика в качестве одной из подсказок
/// к нахождению вакцины.
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
