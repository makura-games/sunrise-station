using Content.Shared._Sunrise.Disease.Analysis;
using Content.Shared._Sunrise.Disease.Vaccinator;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Materials;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>
/// Машина, которая синтезирует вакцину по выбранному антигену и выделяет сыворотку из крови переболевшего.
/// </summary>
/// <remarks>
/// Решения принимает сервер, но всё, что показывает панель, лежит в состоянии компонента.
/// Благодаря этому окно рисуется из уже имеющихся у клиента данных и обновляется само,
/// а таймер синтеза и перезарядки клиент отсчитывает локально, без запросов к серверу.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class VaccinatorComponent : Component
{
    /// <summary>
    /// Идентификатор слота для мазка со штаммом.
    /// </summary>
    [DataField]
    public string SampleSlotId = "vaccinatorSample";

    /// <summary>
    /// Слот для мазка со штаммом.
    /// </summary>
    [DataField]
    public ItemSlot SampleSlot = new();

    /// <summary>
    /// Идентификатор слота для ёмкости с кровью донора.
    /// </summary>
    [DataField]
    public string BloodSlotId = "vaccinatorBlood";

    /// <summary>
    /// Слот для ёмкости с кровью донора.
    /// </summary>
    [DataField]
    public ItemSlot BloodSlot = new();

    /// <summary>
    /// Сколько длится один цикл. Ошибку в выборе партии делает дорогой не время,
    /// а расход образца и сырья, поэтому цикл короткий: найденный рецепт надо успеть размножить.
    /// </summary>
    [DataField]
    public TimeSpan SynthesisDelay = TimeSpan.FromSeconds(12);

    /// <summary>
    /// Прототип готовой вакцины: лечит и даёт иммунитет.
    /// </summary>
    [DataField]
    public EntProtoId VaccinePrototype = "SunriseDiseaseVaccine";

    /// <summary>
    /// Прототип сыворотки из крови донора: только лечит.
    /// </summary>
    [DataField]
    public EntProtoId SerumPrototype = "SunriseDiseaseSerum";

    /// <summary>
    /// Прототип брака, который выдаётся при неподходящем антигене.
    /// </summary>
    [DataField]
    public EntProtoId FailurePrototype = "SunriseDiseaseFailedVaccine";

    /// <summary>
    /// Сколько крови уходит на одну дозу сыворотки.
    /// Не больше шприца: иначе кровь нельзя донести одним забором.
    /// </summary>
    [DataField]
    public float BloodCost = 15f;

    /// <summary>
    /// Сырьё, из которого машина растит препарат.
    /// </summary>
    [DataField]
    public ProtoId<MaterialPrototype> Material = "Biomass";

    /// <summary>
    /// Сколько сырья уходит на пробную партию, удачную или нет.
    /// </summary>
    /// <remarks>
    /// Проба дороже тиража намеренно: перебор вариантов должен стоить заметно больше,
    /// чем производство уже найденного рецепта.
    /// </remarks>
    [DataField]
    public int TrialCost = 15;

    /// <summary>
    /// Сколько сырья уходит на дозу по известному рецепту и на выделение сыворотки.
    /// </summary>
    [DataField]
    public int ProductionCost = 5;

    /// <summary>
    /// Сколько машина промывается после неудачной партии.
    /// </summary>
    /// <remarks>
    /// Главный ограничитель перебора: без него угадать вслепую дешевле, чем исследовать штамм.
    /// </remarks>
    [DataField]
    public TimeSpan FailureCooldown = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Звук работы машины.
    /// </summary>
    [DataField]
    public SoundSpecifier? RunningSound = new SoundPathSpecifier("/Audio/Machines/vaccinator_running.ogg");

    #region Состояние панели

    /// <summary>
    /// Вставлен ли образец, по которому машина знает штамм.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool HasSample;

    /// <summary>
    /// Код штамма из вставленного образца.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public string StrainCode = string.Empty;

    /// <summary>
    /// Пробные партии вакцины. Пусто, пока данных анализов не хватает на их проверку.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<DiseaseVaccineCandidate> Candidates = new();

    /// <summary>
    /// Код подошедшей партии, если рецепт уже найден.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public string? SolvedCode;

    /// <summary>
    /// Накопленные очки исследования штамма.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int Research;

    /// <summary>
    /// Сколько очков нужно, чтобы расшифровать все свойства партий.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int ResearchGoal;

    /// <summary>
    /// Был ли по штамму хоть один отчёт диагностера.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool HasData;

    /// <summary>
    /// Скольких фактов не хватает, чтобы собрать партии.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int MissingFacts;

    /// <summary>
    /// Что машина нашла в слоте крови.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public DiseaseSerumStatus SerumStatus;

    /// <summary>
    /// Коды штаммов, против которых годится кровь во вставленной ёмкости.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public List<string> SerumOptions = new();

    /// <summary>
    /// Запас сырья в машине. Дублируется сюда, чтобы панель обновлялась одним состоянием.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int MaterialAmount;

    /// <summary>
    /// Идёт ли промывка после неудачной партии, а не обычный цикл.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool Cleaning;

    /// <summary>
    /// Когда закончится текущий цикл синтеза. Null, если машина свободна.
    /// </summary>
    /// <remarks>
    /// По этому времени клиент сам рисует обратный отсчёт и снимает блокировку кнопок.
    /// </remarks>
    [ViewVariables, AutoNetworkedField]
    public TimeSpan? FinishAt;

    #endregion
}

/// <summary>
/// Вакцинатор, который сейчас занят синтезом.
/// </summary>
[RegisterComponent]
public sealed partial class ActiveVaccinatorComponent : Component
{
    /// <summary>
    /// Когда закончится синтез.
    /// </summary>
    [ViewVariables]
    public TimeSpan FinishAt;

    /// <summary>
    /// Что именно будет выдано по окончании.
    /// </summary>
    [ViewVariables]
    public EntProtoId Result;

    /// <summary>
    /// Промывка после неудачи: по её окончании машина ничего не выдаёт.
    /// </summary>
    [ViewVariables]
    public bool Cleaning;

    /// <summary>
    /// Болезнь, против которой заряжается препарат. Null для брака.
    /// </summary>
    [ViewVariables]
    public EntityUid? Disease;

    /// <summary>
    /// Код штамма для этикетки.
    /// </summary>
    [ViewVariables]
    public string StrainCode = string.Empty;
}
