using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Тело, в котором при гниении может завестись зараза.
/// </summary>
/// <remarks>
/// Речь не о болезни, от которой существо умерло, а о новой: в разлагающейся плоти заводится то,
/// что в ней живёт, независимо от причины смерти. Поэтому список тут микробный и паразитический —
/// вирусу нужен живой хозяин. Бросок делается один раз, когда тело начинает гнить, и дальше
/// труп работает обычным очагом: виден в визоре, заражает при касании, чистится дезинфектантом.
/// Висит в shared только ради загрузки прототипов на клиенте, логика серверная.
/// </remarks>
[RegisterComponent]
public sealed partial class DiseaseRotSourceComponent : Component
{
    /// <summary>
    /// Болезни, из которых выбирается одна.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<DiseasePrototype>> Diseases = new();

    /// <summary>
    /// Шанс, что в теле вообще что-то заведётся.
    /// </summary>
    [DataField]
    public float Chance = 0.25f;

    /// <summary>
    /// Шанс заражения при каждом касании такого тела.
    /// </summary>
    [DataField]
    public float ContactChance = 0.15f;
}
