using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Events;

/// <summary>
/// Поднимается на возможном носителе перед броском на заражение, затем релеится на надетую экипировку.
/// Подписчики уменьшают <see cref="Multiplier"/>, чтобы снизить шанс, или ставят <see cref="Cancelled"/>,
/// чтобы заблокировать воздействие целиком.
/// </summary>
/// <param name="Disease">Болезнь, действию которой подвергается носитель.</param>
/// <param name="Route">Путь передачи.</param>
/// <param name="Source">Заражённый носитель или предмет, если есть.</param>
/// <param name="Contact">Чем произошёл контакт. Имеет смысл только для <see cref="DiseaseTransmissionRoute.Contact"/>.</param>
[ByRefEvent]
public record struct DiseaseExposureAttemptEvent(
    EntityUid Disease,
    DiseaseTransmissionRoute Route,
    EntityUid? Source,
    DiseaseContactKind Contact = DiseaseContactKind.Touch) : IInventoryRelayEvent
{
    /// <summary>
    /// Множитель шанса заражения.
    /// </summary>
    public float Multiplier = 1f;

    /// <summary>
    /// Заблокировано ли воздействие полностью.
    /// </summary>
    public bool Cancelled;

    /// <inheritdoc/>
    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
}

/// <summary>
/// Поднимается на носителе после того, как он подхватил болезнь.
/// </summary>
[ByRefEvent]
public readonly record struct DiseaseInfectedEvent(
    EntityUid Disease,
    DiseaseTransmissionRoute Route,
    EntityUid? Source);

/// <summary>
/// Поднимается на носителе после перехода одной из его болезней на другую стадию.
/// Если <paramref name="NewStage"/> меньше прежней, болезнь откатывается при выздоровлении.
/// </summary>
[ByRefEvent]
public readonly record struct DiseaseStageChangedEvent(
    EntityUid Disease,
    int OldStage,
    int NewStage);

/// <summary>
/// Поднимается на носителе после того, как у одной из его болезней проявился симптом.
/// </summary>
[ByRefEvent]
public readonly record struct DiseaseSymptomOnsetEvent(
    EntityUid Disease,
    ProtoId<DiseaseSymptomPrototype> Symptom);

/// <summary>
/// Поднимается на носителе после того, как симптом одной из его болезней ушёл
/// при выздоровлении или лечении.
/// </summary>
[ByRefEvent]
public readonly record struct DiseaseSymptomResolvedEvent(
    EntityUid Disease,
    ProtoId<DiseaseSymptomPrototype> Symptom);

/// <summary>
/// Поднимается на носителе после того, как одна из его болезней начала отступать к выздоровлению.
/// </summary>
[ByRefEvent]
public readonly record struct DiseaseRecoveryStartedEvent(
    EntityUid Disease,
    DiseaseCureReason Reason);

/// <summary>
/// Поднимается на носителе после того, как одну из его болезней сняли.
/// </summary>
[ByRefEvent]
public readonly record struct DiseaseCuredEvent(
    EntityUid Disease,
    DiseaseCureReason Reason);

/// <summary>
/// Поднимается на мазке, когда им взяли образец с цели.
/// </summary>
/// <remarks>
/// Нужно, чтобы сам мазок остался общим для клиента и сервера: do-after, отметка об использовании
/// и сообщения предсказываются, а записать, что попало на мазок, может только сервер —
/// болезни живут там.
/// </remarks>
/// <param name="User">Кто взял мазок.</param>
/// <param name="Target">С кого или с чего взяли образец.</param>
[ByRefEvent]
public readonly record struct DiseaseSwabTakenEvent(EntityUid User, EntityUid Target);

/// <summary>
/// Поднимается на привитом в момент укола, а не по окончании выздоровления.
/// </summary>
/// <remarks>
/// Нужно для болезней, у которых прививка делает что-то сверх иммунитета: например,
/// зомби-вакцина обязана отменить обращение немедленно, иначе укушенный успеет обратиться.
/// </remarks>
/// <param name="Disease">Штамм, против которого привили.</param>
[ByRefEvent]
public readonly record struct DiseaseVaccinatedEvent(EntityUid Disease);
