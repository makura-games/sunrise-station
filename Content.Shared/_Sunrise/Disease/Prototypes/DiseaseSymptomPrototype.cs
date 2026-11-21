using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// Переиспользуемый симптом болезни. Фиксированного набора симптомов у болезней нет: каждая стадия
/// вытягивает их из взвешенного пула, поэтому одна и та же болезнь у разных носителей выглядит по-разному.
/// </summary>
[Prototype]
public sealed partial class DiseaseSymptomPrototype : IPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Название симптома, которое показывает анализ образца.
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Текст утверждения «подавляет этот симптом» для карточек партий вакцинатора.
    /// </summary>
    /// <remarks>
    /// Лежит в самом симптоме, а не в отдельном прототипе утверждений: в русском каждая формулировка
    /// требует своего падежа, поэтому собрать фразу из названия симптома по шаблону не выйдет.
    /// </remarks>
    [DataField(required: true)]
    public LocId SuppressClaim;

    /// <summary>
    /// Текст утверждения «нестабилен при этом симптоме».
    /// </summary>
    [DataField(required: true)]
    public LocId UnstableClaim;

    /// <summary>
    /// Эффекты, которые применяются к носителю каждый раз, когда симптом срабатывает.
    /// Лучше брать готовые: <c>Emote</c>, <c>Vomit</c>, <c>PopupMessage</c>, <c>HealthChange</c> или <c>ModifyStatusEffect</c>.
    /// </summary>
    [DataField(required: true)]
    public EntityEffect[] Effects = default!;

    /// <summary>
    /// Эффекты, которые применяются один раз в момент появления симптома у носителя.
    /// </summary>
    [DataField]
    public EntityEffect[]? OnsetEffects;

    /// <summary>
    /// Шанс срабатывания симптома на каждом шаге обработки болезни.
    /// </summary>
    /// <remarks>
    /// Бросается серверным генератором. Лучше задавать здесь, чем через <c>probability</c> у эффектов:
    /// там бросок завязан на номер тика, и эффекты с одинаковым шансом срабатывают разом.
    /// </remarks>
    [DataField]
    public float Probability = 1f;

    /// <summary>
    /// Насколько симптом тяжёл для носителя. В отличие от <see cref="DiseaseSymptomEntry.Weight"/>,
    /// который влияет только на частоту выпадения, это идёт в оценку силы болезни,
    /// а та задаёт размер загадки с вакциной.
    /// </summary>
    [DataField]
    public float Severity = 1f;

    /// <summary>
    /// Скрывает ли этот симптом подавляющее лечение.
    /// </summary>
    [DataField]
    public bool Suppressible = true;

    /// <summary>
    /// Полезный симптом: даёт носителю бафф, а не вред.
    /// </summary>
    /// <remarks>
    /// В природных болезнях такие симптомы не проявляются: реестр вычищает их из пулов стадий.
    /// Получить их можно только под микроскопом — изредка заготовка кладёт один на последнюю стадию.
    /// </remarks>
    [DataField]
    public bool Beneficial;

    /// <summary>
    /// Выброс капель вместе с симптомом, например при кашле. Необязателен.
    /// </summary>
    [DataField]
    public DiseaseSpread? Spread;
}
