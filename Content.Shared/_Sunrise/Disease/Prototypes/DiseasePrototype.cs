using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.Disease.Prototypes;

/// <summary>
/// Неизменяемое описание болезни: стадии, симптомы, передача, лечение и данные для диагностики.
/// Ход болезни у конкретного носителя здесь не хранится — он живёт в серверном компоненте носителя.
/// </summary>
[Prototype]
public sealed partial class DiseasePrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc/>
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <inheritdoc/>
    /// <remarks>
    /// Наследование нужно архетипам-заготовкам: несколько болезней отличаются только идентификатором,
    /// а рамка у них общая.
    /// </remarks>
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<DiseasePrototype>))]
    public string[]? Parents { get; private set; }

    /// <inheritdoc/>
    [NeverPushInheritance, AbstractDataField]
    public bool Abstract { get; private set; }

    #region Diagnostics

    /// <summary>
    /// Название болезни для админских инструментов и гайдбука. Анализ образца его не раскрывает:
    /// игроки опознают болезнь по коду штамма и замеченным симптомам.
    /// </summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Описание для админских инструментов и гайдбука.
    /// </summary>
    [DataField]
    public LocId? Description;

    /// <summary>
    /// Синдром, который показывает любой анализ, даже самый грубый.
    /// Описывает, как болезнь выглядит.
    /// </summary>
    [DataField]
    public LocId Syndrome = "disease-syndrome-unknown";

    /// <summary>
    /// Класс возбудителя, который раскрывает анализ уровня классификации.
    /// </summary>
    [DataField]
    public DiseaseClassification Classification = DiseaseClassification.Viral;

    /// <summary>
    /// Подсказки по лечению, которые раскрывает точный анализ.
    /// </summary>
    [DataField]
    public List<LocId> TreatmentHints = new();

    #endregion

    #region Progression

    /// <summary>
    /// Стадии болезни по порядку. Должна быть хотя бы одна.
    /// </summary>
    [DataField(required: true)]
    public List<DiseaseStage> Stages = new();

    /// <summary>
    /// Как часто бросаются проявившиеся симптомы и проверяется смена стадии.
    /// </summary>
    [DataField]
    public TimeSpan ProcessInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Перекатывает ли штамм раунда то, какие симптомы из пулов стадий у болезни вообще будут.
    /// Выключается для болезней, которые должны оставаться узнаваемыми, вроде отыгрышной простуды.
    /// </summary>
    [DataField]
    public bool RollStrain = true;

    /// <summary>
    /// Сколько разных симптомов должно быть у штамма хотя бы в сумме по стадиям.
    /// </summary>
    /// <remarks>
    /// Если бросок стадий дал меньше, набор добирается из непопавших записей пулов.
    /// Меньше нельзя: по симптомам строятся свойства пробных партий в микроскопе,
    /// и на пустом наборе загадка не соберётся.
    /// </remarks>
    [DataField]
    public int MinStrainSymptoms = 3;

    /// <summary>
    /// Как быстро отступающая болезнь откатывается на стадию назад. Каждый шаг назад снимает симптомы
    /// покинутой стадии, а шаг с нулевой стадии удаляет болезнь.
    /// </summary>
    [DataField]
    public DiseaseProgression Regression = new()
    {
        MinDuration = TimeSpan.FromSeconds(20),
        Chance = 0.25f,
        MaxDuration = TimeSpan.FromSeconds(60),
    };

    #endregion

    #region Transmission

    /// <summary>
    /// Пути передачи, доступные этой болезни. Попытка заразить другим путём не пройдёт.
    /// </summary>
    [DataField]
    public DiseaseTransmissionRoute Routes = DiseaseTransmissionRoute.None;

    /// <summary>
    /// Симптом с каплями, который выдаётся штамму с воздушным путём, если своего не досталось.
    /// </summary>
    /// <remarks>
    /// Болезнь, которая передаётся по воздуху, обязана проявляться кашлем или чиханием: иначе
    /// носителя нельзя опознать, а путь передачи не работает вовсе. Сначала ищется подходящий
    /// симптом в пулах самого архетипа, и только потом берётся этот.
    /// </remarks>
    [DataField]
    public ProtoId<DiseaseSymptomPrototype> DropletFallbackSymptom = "SymptomCough";

    /// <summary>
    /// Если задан, заболеть могут только подходящие под него сущности.
    /// </summary>
    [DataField]
    public EntityWhitelist? Whitelist;

    /// <summary>
    /// Подходящие под него сущности не заболевают никогда.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;

    #endregion

    #region Treatment

    /// <summary>
    /// Сколько прогресса лечения нужно набрать, чтобы началось выздоровление. Лечебные эффекты
    /// добавляют прогресс по своей силе, поэтому болезнь может требовать курса, а не одной дозы.
    /// </summary>
    [DataField]
    public float CureThreshold = 1f;

    /// <summary>
    /// Сколько нужно проспать, чтобы вылечиться с нуля. Ноль — сон от этой болезни не помогает.
    /// </summary>
    /// <remarks>
    /// Выражено временем, а не скоростью, чтобы в прототипе читалось сразу: «отлежаться три минуты».
    /// Внутри переводится в прогресс лечения, поэтому сон и лекарства складываются: проспавший половину
    /// срока добирает остальное дозой. Лечит и подавленную болезнь — подавление тормозит развитие,
    /// а не выздоровление. Годится только лёгким болезням: от тяжёлых организм сам не отобьётся.
    /// </remarks>
    [DataField]
    public TimeSpan SleepCureTime = TimeSpan.Zero;

    /// <summary>
    /// Останавливает ли подавляющее лечение ещё и переход на следующую стадию.
    /// Выздоровление не останавливается никогда.
    /// </summary>
    [DataField]
    public bool SuppressionPausesProgression = true;

    /// <summary>
    /// Множитель капельной и контактной передачи, пока болезнь подавлена.
    /// </summary>
    [DataField]
    public float SuppressedSpreadMultiplier;

    /// <summary>
    /// Иммунитет, который выдаётся после лечения или естественного выздоровления.
    /// </summary>
    [DataField]
    public DiseaseImmunityPolicy Immunity = new();

    #endregion
}
