using Content.Server._Sunrise.StationEvents.Events;
using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.StationEvents.Components;

/// <summary>
/// Вспышка болезни: несколько членов экипажа просыпаются уже заражёнными.
/// </summary>
/// <remarks>
/// Нулевой пациент не знает, что болен: первые минуты идёт инкубация без симптомов,
/// и вспышка начинается тихо.
/// </remarks>
[RegisterComponent, Access(typeof(DiseaseOutbreakRule))]
public sealed partial class DiseaseOutbreakRuleComponent : Component
{
    /// <summary>
    /// Болезни, из которых выбирается одна на всю вспышку.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<DiseasePrototype>> Diseases = new();

    /// <summary>
    /// Сколько человек заражается минимум.
    /// </summary>
    [DataField]
    public int MinInfected = 1;

    /// <summary>
    /// Сколько человек заражается максимум.
    /// </summary>
    [DataField]
    public int MaxInfected = 2;
}
