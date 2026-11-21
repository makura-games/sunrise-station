using Content.Server._Sunrise.Xenoarchaeology.Artifact.XAE;
using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Xenoarchaeology.Artifact.XAE.Components;

/// <summary>
/// Артефакт выбрасывает облако заразного аэрозоля: всё живое вокруг дышит им и заболевает.
/// </summary>
/// <remarks>
/// Болезнь выбирается один раз при появлении узла и дальше не меняется: вспышка от одного
/// артефакта должна быть вспышкой одного штамма, иначе медотдел получит кашу из разных болезней.
/// </remarks>
[RegisterComponent, Access(typeof(XAEDiseaseCloudSystem))]
public sealed partial class XAEDiseaseCloudComponent : Component
{
    /// <summary>
    /// Болезни, из которых выбирается одна.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<DiseasePrototype>> Diseases = new();

    /// <summary>
    /// Выбранная болезнь.
    /// </summary>
    [DataField]
    public ProtoId<DiseasePrototype>? SelectedDisease;

    /// <summary>
    /// Штамм этого узла, заведённый при первом срабатывании.
    /// </summary>
    /// <remarks>
    /// Свой у каждого артефакта: второй артефакт с тем же прототипом болезни даёт другую вспышку,
    /// и вакцина от первого её не закрывает.
    /// </remarks>
    [ViewVariables]
    public EntityUid? Strain;

    /// <summary>
    /// Прототип облака.
    /// </summary>
    [DataField]
    public EntProtoId CloudPrototype = "SunriseDiseaseCloud";

    /// <summary>
    /// Сколько облаков выбрасывается за одно срабатывание.
    /// </summary>
    [DataField]
    public int Clouds = 3;

    /// <summary>
    /// В каком радиусе от артефакта расходятся облака.
    /// </summary>
    [DataField]
    public float SpreadRange = 2.5f;
}
