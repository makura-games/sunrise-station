using Content.Server._Sunrise.Disease.Systems;

namespace Content.Server._Sunrise.Disease.Components;

/// <summary>
/// Штамм зомби-вспышки. Висит на сущности игрового правила, а не на заражённых.
/// </summary>
/// <remarks>
/// Нулевые пациенты правила берут штамм отсюда, дальше он расходится по укусам.
/// Поэтому вторая зомби-вспышка за раунд требует своей вакцины.
/// </remarks>
[RegisterComponent, Access(typeof(DiseaseZombieSystem))]
public sealed partial class DiseaseZombieStrainComponent : Component
{
    /// <summary>Болезнь этой вспышки, заводится при первом заражении.</summary>
    [ViewVariables]
    public EntityUid? Strain;
}
