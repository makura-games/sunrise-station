using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Disease.Components;

/// <summary>
/// Экземпляр болезни: всё, чем она опасна для станции, и набор симптомов именно этого штамма.
/// </summary>
/// <remarks>
/// Болезнь живёт как сущность в нигде, по образцу игровых правил: прототип задаёт только рамку,
/// а штамм собирается в рантайме. Поэтому вирусолог может вывести болезнь, которой нет в прототипах,
/// а природная болезнь каждый раунд получает свой набор симптомов.
/// Компонент серверный: клиенту состояние болезней не нужно, он видит только последствия симптомов.
/// </remarks>
[RegisterComponent]
public sealed partial class DiseaseDefinitionComponent : Component
{
    /// <summary>Выведен ли этот штамм в селекционном микроскопе.</summary>
    public bool Designed;

    /// <summary>Устойчивость, унаследованная от заготовки, из которой выведен штамм; от нуля до единицы.</summary>
    public float Resistance;

    /// <summary>
    /// Прототип, по которому собрана болезнь. У выведенных штаммов это архетип-рамка.
    /// </summary>
    [DataField]
    public ProtoId<DiseasePrototype>? Template;

    /// <summary>
    /// Код штамма: по нему станция связывает образцы, отчёты и вакцины.
    /// </summary>
    [DataField]
    public string StrainCode = string.Empty;

    #region Diagnostics

    /// <inheritdoc cref="DiseasePrototype.Name"/>
    [DataField]
    public LocId Name;

    /// <inheritdoc cref="DiseasePrototype.Description"/>
    [DataField]
    public LocId? Description;

    /// <inheritdoc cref="DiseasePrototype.Syndrome"/>
    [DataField]
    public LocId Syndrome = "disease-syndrome-unknown";

    /// <inheritdoc cref="DiseasePrototype.Classification"/>
    [DataField]
    public DiseaseClassification Classification = DiseaseClassification.Viral;

    /// <inheritdoc cref="DiseasePrototype.TreatmentHints"/>
    [DataField]
    public List<LocId> TreatmentHints = new();

    #endregion

    #region Progression

    /// <summary>
    /// Стадии этого штамма: форма взята из прототипа, а пулы симптомов уже прокатаны.
    /// </summary>
    [DataField]
    public List<DiseaseStage> Stages = new();

    /// <inheritdoc cref="DiseasePrototype.ProcessInterval"/>
    [DataField]
    public TimeSpan ProcessInterval = TimeSpan.FromSeconds(5);

    /// <inheritdoc cref="DiseasePrototype.Regression"/>
    [DataField]
    public DiseaseProgression Regression = new();

    #endregion

    #region Transmission

    /// <inheritdoc cref="DiseasePrototype.Routes"/>
    [DataField]
    public DiseaseTransmissionRoute Routes = DiseaseTransmissionRoute.None;

    /// <inheritdoc cref="DiseasePrototype.Whitelist"/>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <inheritdoc cref="DiseasePrototype.Blacklist"/>
    [DataField]
    public EntityWhitelist? Blacklist;

    #endregion

    #region Treatment

    /// <inheritdoc cref="DiseasePrototype.CureThreshold"/>
    [DataField]
    public float CureThreshold = 1f;

    /// <inheritdoc cref="DiseasePrototype.SleepCureTime"/>
    [DataField]
    public TimeSpan SleepCureTime = TimeSpan.Zero;

    /// <inheritdoc cref="DiseasePrototype.SuppressionPausesProgression"/>
    [DataField]
    public bool SuppressionPausesProgression = true;

    /// <inheritdoc cref="DiseasePrototype.SuppressedSpreadMultiplier"/>
    [DataField]
    public float SuppressedSpreadMultiplier;

    /// <inheritdoc cref="DiseasePrototype.Immunity"/>
    [DataField]
    public DiseaseImmunityPolicy Immunity = new();

    #endregion

    /// <summary>
    /// Все симптомы, которые штамм способен вызвать хотя бы на одной стадии.
    /// </summary>
    [ViewVariables]
    public readonly HashSet<ProtoId<DiseaseSymptomPrototype>> Symptoms = new();

    /// <summary>
    /// Симптом с каплями, который каждый носитель обязательно получает на стадии <see cref="DropletStage"/>.
    /// </summary>
    /// <remarks>
    /// Капли разлетаются только от кашля, чиха и подобных симптомов. Без гарантии штамм
    /// с воздушным путём мог бы не выбросить ни одного такого симптома, и путь был бы мёртвым.
    /// </remarks>
    [ViewVariables]
    public ProtoId<DiseaseSymptomPrototype>? DropletSymptom;

    /// <summary>
    /// Стадия, на которой носитель получает <see cref="DropletSymptom"/>. -1 — гарантии нет.
    /// </summary>
    [ViewVariables]
    public int DropletStage = -1;

    /// <summary>
    /// ДНК тех, кто переболел этим штаммом: их кровь годится для сыворотки.
    /// </summary>
    /// <remarks>
    /// Лежит на болезни, а не в словаре системы: живёт и умирает вместе со штаммом, поэтому не
    /// нуждается в уборке на рестарте раунда и виден в ViewVariables рядом с остальными свойствами.
    /// Хранится именно ДНК, а не донор: кровь остаётся пригодной, когда самого донора уже нет.
    /// </remarks>
    [ViewVariables]
    public readonly HashSet<string> Donors = new();

    /// <summary>
    /// Сила штамма: заразность плюс тяжесть симптомов. Считается при первом обращении, null — ещё не считали.
    /// </summary>
    /// <remarks>
    /// Лежит на самом штамме, а не в кеше системы: живёт и умирает вместе с сущностью, поэтому
    /// не нуждается в уборке на рестарте раунда и виден в ViewVariables рядом с остальными
    /// свойствами болезни. Пересчёт не нужен: после сборки штамма ни пути передачи, ни стадии
    /// уже не меняются.
    /// </remarks>
    [ViewVariables]
    public int? Strength;
}
