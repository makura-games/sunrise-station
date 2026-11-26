using Content.Server._Sunrise.Disease.Systems;
using Content.Shared._Sunrise.Disease.Events;
using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Disease.Components;

/// <summary>
/// Носитель болезней. Появляется при первом заражении и снимается, когда ушла последняя болезнь,
/// поэтому цикл развития обходит только больных.
/// </summary>
/// <remarks>
/// Система серверная для защиты данной информации от клиента.
/// </remarks>
[RegisterComponent, AutoGenerateComponentPause, Access(typeof(DiseaseSystem))]
public sealed partial class DiseaseCarrierComponent : Component
{
    /// <summary>
    /// Болезни, которыми носитель болен сейчас. Одна и та же болезнь дважды сюда не попадает.
    /// </summary>
    [ViewVariables]
    public List<DiseaseInstance> Diseases = new();

    /// <summary>
    /// Когда носителя обработают в следующий раз.
    /// </summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan NextUpdate;
}

/// <summary>
/// Ход одной болезни у одного носителя.
/// </summary>
public sealed class DiseaseInstance(EntityUid disease, TimeSpan infectedAt)
{
    /// <summary>
    /// Сущность болезни.
    /// </summary>
    [ViewVariables]
    public EntityUid Disease = disease;

    /// <summary>
    /// Номер текущей стадии, считая с нуля.
    /// </summary>
    [ViewVariables]
    public int Stage;

    /// <summary>
    /// Сколько времени носитель провёл на текущей стадии. Пока болезнь подавлена и стоит, не растёт.
    /// </summary>
    [ViewVariables]
    public TimeSpan StageElapsed;

    /// <summary>
    /// Симптомы, проявившиеся у этого носителя. Каждый помнит стадию, на которой его вытянули,
    /// чтобы выздоровление сняло его, когда болезнь откатится ниже этой стадии.
    /// </summary>
    [ViewVariables]
    public List<DiseaseActiveSymptom> Symptoms = new();

    /// <summary>
    /// Отступает ли болезнь к полному выздоровлению.
    /// </summary>
    [ViewVariables]
    public bool Recovering;

    /// <summary>
    /// Почему началось выздоровление. Передаётся в итоговое излечение, которое решает про иммунитет.
    /// </summary>
    [ViewVariables]
    public DiseaseCureReason RecoveryReason;

    /// <summary>
    /// Время, накопленное до следующего броска симптомов.
    /// </summary>
    [ViewVariables]
    public TimeSpan ProcessAccumulator;

    /// <summary>
    /// Сколько ещё держится подавление. Ноль — болезнь не подавлена.
    /// </summary>
    [ViewVariables]
    public TimeSpan SuppressionRemaining;

    /// <summary>
    /// Сколько ещё держится снятие симптомов. В отличие от подавления, оно только глушит проявления
    /// и не трогает ни развитие, ни заразность.
    /// </summary>
    [ViewVariables]
    public TimeSpan ReliefRemaining;

    /// <summary>
    /// Накопленный прогресс лечения.
    /// </summary>
    [ViewVariables]
    public float CureProgress;

    /// <summary>
    /// Время раунда, когда произошло заражение.
    /// </summary>
    [ViewVariables]
    public TimeSpan InfectedAt = infectedAt;

    /// <summary>
    /// Подавлена ли болезнь сейчас.
    /// </summary>
    public bool Suppressed => SuppressionRemaining > TimeSpan.Zero;

    /// <summary>
    /// Заглушены ли сейчас симптомы болезни.
    /// </summary>
    public bool Relieved => ReliefRemaining > TimeSpan.Zero;
}

/// <summary>
/// Симптом, проявившийся у носителя.
/// </summary>
/// <param name="Symptom">Прототип симптома.</param>
/// <param name="Stage">Стадия, на которой симптом был вытянут, считая с нуля.</param>
public readonly record struct DiseaseActiveSymptom(ProtoId<DiseaseSymptomPrototype> Symptom, int Stage);
