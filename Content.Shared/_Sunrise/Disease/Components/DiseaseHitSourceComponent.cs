using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Заражает тем, что ранит: заразу переносит удар, а не прикосновение.
/// </summary>
/// <remarks>
/// Висит на том, чем бьют. Это либо само животное — тогда заражают его укусы и когти,
/// либо оружие в руках — тогда заразу несёт клинок, чей угодно. Носитель компонента сам
/// не болеет и симптомов не показывает: болезнь он только передаёт, поэтому мышь из вентиляции
/// не отличить от обычной, а грязный нож выглядит как обычный нож.
/// Висит в shared только ради загрузки прототипов на клиенте, логика серверная.
/// </remarks>
[RegisterComponent]
public sealed partial class DiseaseHitSourceComponent : Component
{
    /// <summary>Болезни, из которых при первом удачном ударе выбирается одна на весь предмет.</summary>
    [DataField(required: true)]
    public List<ProtoId<DiseasePrototype>> Diseases = new();

    /// <summary>Шанс заражения с удара, до учёта экипировки цели.</summary>
    [DataField]
    public float Chance = 0.1f;

    /// <summary>
    /// Чем считается такой удар для экипировки цели.
    /// </summary>
    /// <remarks>
    /// По умолчанию <see cref="DiseaseContactKind.Bite"/>: кожа пробита, и спасает только то,
    /// что закрывает тело целиком, вроде скафандра. Для оружия, которое не ранит, а размазывает
    /// заразу по коже, подойдёт <see cref="DiseaseContactKind.Touch"/> — тогда от него закроют
    /// и перчатки.
    /// </remarks>
    [DataField]
    public DiseaseContactKind Contact = DiseaseContactKind.Bite;

    /// <summary>Штамм, общий для всех ударов этого источника.</summary>
    public EntityUid? Strain;
}
