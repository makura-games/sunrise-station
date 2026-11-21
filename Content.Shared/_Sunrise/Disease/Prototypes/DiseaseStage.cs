using Content.Shared.EntityEffects;

namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// Одна стадия болезни. Стадии идут по порядку; нулевая — обычно инкубация без симптомов.
/// При выздоровлении болезнь, наоборот, откатывается по стадиям назад.
/// </summary>
[DataDefinition]
public sealed partial class DiseaseStage
{
    /// <summary>
    /// Когда болезнь перейдёт на следующую стадию. На последней это запускает естественное выздоровление,
    /// а нулевой шанс без максимума делает болезнь хронической.
    /// </summary>
    [DataField]
    public DiseaseProgression Advance = new();

    /// <summary>
    /// Симптомы, которые могут проявиться при входе носителя в эту стадию.
    /// Симптомы, вытянутые на прежних стадиях, остаются.
    /// </summary>
    [DataField]
    public List<DiseaseSymptomEntry> SymptomPool = new();

    /// <summary>
    /// Сколько симптомов из <see cref="SymptomPool"/> достанется штамму этого раунда.
    /// Ноль оставляет весь пул, и тогда стадия каждый раунд одинаковая.
    /// </summary>
    /// <remarks>
    /// Это бросок на раунд, а не на носителя: штамм решает, что болезнь вообще умеет,
    /// а <see cref="MinSymptoms"/> и <see cref="MaxSymptoms"/> — что из этого покажет конкретный носитель.
    /// </remarks>
    [DataField]
    public int RollCount;

    /// <summary>
    /// Сколько новых симптомов из <see cref="SymptomPool"/> выпадет при входе в стадию минимум.
    /// </summary>
    [DataField]
    public int MinSymptoms = 1;

    /// <summary>
    /// Сколько новых симптомов из <see cref="SymptomPool"/> выпадет при входе в стадию максимум.
    /// </summary>
    [DataField]
    public int MaxSymptoms = 1;

    /// <summary>
    /// Эффекты, которые применяются один раз при переходе болезни на эту стадию.
    /// </summary>
    [DataField]
    public EntityEffect[]? EnterEffects;

    /// <summary>
    /// Множитель шанса капельной передачи для симптомов, пока болезнь на этой стадии.
    /// </summary>
    [DataField]
    public float SpreadMultiplier = 1f;

    /// <summary>
    /// Шанс заразить другую сущность при каждом касании носителя.
    /// Ноль отключает контактную передачу от носителя к носителю на этой стадии.
    /// </summary>
    [DataField]
    public float ContactSpreadChance;
}
