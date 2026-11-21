using Content.Shared._Sunrise.Disease.Prototypes;

namespace Content.Shared._Sunrise.Disease;

/// <summary>
/// Сопоставляет перечисления болезней с ключами локализации: гайдбук, результаты анализа, админ-инструменты.
/// </summary>
public static class DiseaseLocalization
{
    /// <summary>
    /// Возвращает ключ локализации для заявления вакцины о классе возбудителя.
    /// </summary>
    public static LocId GetClassClaimLocId(DiseaseClassification classification)
    {
        return classification switch
        {
            DiseaseClassification.Viral => "disease-claim-works-on-viruses",
            DiseaseClassification.Bacterial => "disease-claim-works-on-bacteria",
            DiseaseClassification.Fungal => "disease-claim-works-on-fungi",
            DiseaseClassification.Parasitic => "disease-claim-works-on-parasites",
            DiseaseClassification.Environmental => "disease-claim-works-on-environmental",
            DiseaseClassification.Anomalous => "disease-claim-works-on-anomalous",
            _ => "disease-claim-works-on-viruses",
        };
    }

    /// <summary>
    /// Возвращает ключ локализации для названия класса болезней.
    /// </summary>
    public static LocId GetClassificationLocId(DiseaseClassification classification)
    {
        return classification switch
        {
            DiseaseClassification.Viral => "disease-classification-viral",
            DiseaseClassification.Bacterial => "disease-classification-bacterial",
            DiseaseClassification.Fungal => "disease-classification-fungal",
            DiseaseClassification.Parasitic => "disease-classification-parasitic",
            DiseaseClassification.Environmental => "disease-classification-environmental",
            DiseaseClassification.Anomalous => "disease-classification-anomalous",
            _ => "disease-classification-unknown",
        };
    }
}
