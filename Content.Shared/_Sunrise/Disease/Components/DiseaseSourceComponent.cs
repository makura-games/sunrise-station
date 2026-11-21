using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Помечает предмет как заражённый очаг: от него болеет тот, кто его съел или коснулся.
/// </summary>
/// <remarks>
/// Живёт в shared только ради загрузки прототипов на клиенте. Вся логика на сервере.
/// </remarks>
[RegisterComponent]
public sealed partial class DiseaseSourceComponent : Component
{
    /// <summary>
    /// Болезни, которыми заражён очаг.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<DiseasePrototype>> Templates = new();

    /// <summary>
    /// Болезни раунда, которыми заражён источник. Заполняется системой из <see cref="Templates"/>,
    /// поэтому в источник можно подложить и выведенный штамм, которого нет в прототипах.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> Diseases = new();

    /// <summary>
    /// Пути передачи, которыми заражает очаг. Сама болезнь тоже должна поддерживать этот путь.
    /// </summary>
    [DataField]
    public DiseaseTransmissionRoute Routes = DiseaseTransmissionRoute.Ingestion;

    /// <summary>
    /// Шанс заражения с каждого укуса или глотка.
    /// </summary>
    [DataField]
    public float IngestionChance = 1f;

    /// <summary>
    /// Шанс заражения при каждом касании очага.
    /// </summary>
    [DataField]
    public float ContactChance = 0.1f;

    /// <summary>
    /// Шанс заражения для того, кто прошёл по источнику. Ноль — наступать безопасно.
    /// </summary>
    /// <remarks>
    /// Считается контактной передачей и работает только вместе с <c>StepTrigger</c> на сущности:
    /// так заражают наросты на полу, мимо которых экипаж ходит, не трогая их руками.
    /// </remarks>
    [DataField]
    public float StepChance;
}
