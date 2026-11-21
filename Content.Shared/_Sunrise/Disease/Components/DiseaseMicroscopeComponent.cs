using Content.Shared._Sunrise.Disease.Microscope;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Components;

/// <summary>Настройки и реплицированное состояние партии развития штамма.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class DiseaseMicroscopeComponent : Component
{
    /// <summary>Снята ли блокировка безопасности, запирающая развитие путей передачи.</summary>
    [AutoNetworkedField]
    public bool TransmissionUnlocked;

    /// <summary>Пути передачи, развитые во вставленной заготовке.</summary>
    [AutoNetworkedField]
    public DiseaseTransmissionRoute Routes = DiseaseTransmissionRoute.Injection;

    /// <summary>Путь передачи, который развивает текущая партия культуры.</summary>
    [AutoNetworkedField]
    public DiseaseTransmissionRoute TargetRoute;

    /// <summary>Слот с исходной заготовкой.</summary>
    [DataField]
    public string SampleSlotId = "microscopeSample";

    /// <summary>Правила вставки образцов.</summary>
    [DataField]
    public ItemSlot SampleSlot = new();

    /// <summary>Резервуар, питающий обе пипетки.</summary>
    [DataField]
    public string SolutionName = "microscope";

    /// <summary>Реагент, расходуемый на питание культуры.</summary>
    [DataField]
    public ProtoId<ReagentPrototype> NutrientReagent = "EZNutrient";

    /// <summary>Реагент, расходуемый на ингибитор.</summary>
    [DataField]
    public ProtoId<ReagentPrototype> InhibitorReagent = "Phenol";

    /// <summary>
    /// Сколько питательной среды уходит на засев поля.
    /// Перезасев не бесплатен, иначе проигранную партию можно было бы отменять без потерь.
    /// </summary>
    [DataField]
    public int PrepareCost = 5;

    /// <summary>Пробирка, в которую уходит собранный геном.</summary>
    [DataField]
    public EntProtoId OutputPrototype = "SunriseDiseaseSelectedSample";

    /// <summary>Звук принятой операции: засев, прогноз, завершение партии.</summary>
    [DataField]
    public SoundSpecifier SuccessSound = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg")
    {
        Params = AudioParams.Default.WithVolume(-10f),
    };

    /// <summary>Звук капли питательной среды.</summary>
    [DataField]
    public SoundSpecifier NutrientSound = new SoundPathSpecifier("/Audio/Effects/drop.ogg")
    {
        Params = AudioParams.Default.WithVolume(-10f),
    };

    /// <summary>Звук впрыска ингибитора.</summary>
    [DataField]
    public SoundSpecifier InhibitorSound = new SoundPathSpecifier("/Audio/Effects/spray.ogg")
    {
        Params = AudioParams.Default.WithVolume(-10f),
    };

    /// <summary>Звук проигранной партии: колония-конкурент задавила цель.</summary>
    [DataField]
    public SoundSpecifier FailureSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg")
    {
        Params = AudioParams.Default.WithVolume(-10f),
    };

    /// <summary>
    /// Архетип-рамка, по которому лаборатория выпускает выведенные штаммы:
    /// от него болезнь берёт иммунитет и скорость развития, а стадии и пути передачи задаёт вирусолог.
    /// </summary>
    [DataField]
    public ProtoId<DiseasePrototype> DesignedArchetype = "SunriseDesignedStrainBase";

    /// <summary>Исходная заготовка текущего эксперимента; как UID не реплицируется.</summary>
    public EntityUid? SourceSample;

    /// <summary>Есть ли у машины питание.</summary>
    [AutoNetworkedField]
    public bool Powered;

    /// <summary>Лежит ли в слоте пригодная заготовка.</summary>
    [AutoNetworkedField]
    public bool HasSample;

    /// <summary>Лежит ли в слоте вообще какой-нибудь предмет.</summary>
    [AutoNetworkedField]
    public bool SampleInserted;

    /// <summary>Код штамма заготовки.</summary>
    [AutoNetworkedField]
    public string StrainCode = string.Empty;

    /// <summary>Вставленный предмет для показа в окне.</summary>
    [AutoNetworkedField]
    public NetEntity? SampleView;

    /// <summary>Номер развиваемой стадии, считая с нуля.</summary>
    [AutoNetworkedField]
    public int Stage;

    /// <summary>Сколько стадий у заготовки всего.</summary>
    [AutoNetworkedField]
    public int MaxStage;

    /// <summary>Симптомы, доступные для текущей партии: цели стадии либо кандидаты на усиление.</summary>
    [AutoNetworkedField]
    public List<ProtoId<DiseaseSymptomPrototype>> StageOptions = new();

    /// <summary>Последнюю стадию ещё можно усилить вторым симптомом.</summary>
    [AutoNetworkedField]
    public bool StageOpen;

    /// <summary>Идущая партия усиливает уже закрытую стадию, а не открывает новую.</summary>
    [AutoNetworkedField]
    public bool BonusMatch;

    /// <summary>Сколько усилений ещё допускает заготовка: запас общий на весь геном.</summary>
    [AutoNetworkedField]
    public int ReinforcementsLeft;

    /// <summary>Закрытые стадии с их пулами: это и есть вектор развития будущего штамма.</summary>
    [AutoNetworkedField]
    public List<DiseaseStagePool> Genome = new();

    /// <summary>Симптом, который растят в текущей партии.</summary>
    [AutoNetworkedField]
    public ProtoId<DiseaseSymptomPrototype>? TargetSymptom;

    /// <summary>Симптом-конкурент текущей партии. Развить его нельзя, он только мешает.</summary>
    [AutoNetworkedField]
    public ProtoId<DiseaseSymptomPrototype>? RivalSymptom;

    /// <summary>Поле партии построчно. Ноль — пусто, иначе колония или выжженная среда.</summary>
    [AutoNetworkedField]
    public byte[] Cells = [];

    /// <summary>Клетки, которые конкурент занял последним ответом.</summary>
    [AutoNetworkedField]
    public List<int> OpponentMoves = new();

    /// <summary>Сколько ходов сделано в текущей партии.</summary>
    [AutoNetworkedField]
    public int Turn;

    /// <summary>
    /// Сколько партий засеяно на этой машине. Из него и сущности машины берётся сид поля,
    /// поэтому клиент засевает то же поле, что и сервер, а каждая новая партия идёт по-новому.
    /// Заодно отсекает повторное нажатие засева, отправленное по устаревшему состоянию.
    /// </summary>
    [AutoNetworkedField]
    public int MatchCounter;

    /// <summary>
    /// Сколько прогнозов ответа конкурента выдаётся на одну партию.
    /// Каждый прогноз окрашивает мазок и тратит ингибитор по цене хода текущей стадии.
    /// </summary>
    [DataField]
    public int ForecastsPerMatch = 2;

    /// <summary>Сколько прогнозов осталось в текущей партии.</summary>
    [AutoNetworkedField]
    public int ForecastsLeft;

    /// <summary>Прогноз оплачен: до следующего хода предпросмотр капли показывает и ответ конкурента.</summary>
    [AutoNetworkedField]
    public bool ForecastActive;

    /// <summary>Условия текущей партии, выведенные из стадии и устойчивости заготовки.</summary>
    [AutoNetworkedField]
    public DiseaseCultureProfile Profile;

    /// <summary>
    /// Устойчивость вставленной заготовки. Нужна клиенту, чтобы предсказать условия партии за путь
    /// передачи; секретом она не является — профиль партии из неё и так виден.
    /// </summary>
    [AutoNetworkedField]
    public float SampleResistance;

    /// <summary>Число применений питания, доступных в резервуаре.</summary>
    [AutoNetworkedField]
    public int NutrientUnits;

    /// <summary>Число применений ингибитора, доступных в резервуаре.</summary>
    [AutoNetworkedField]
    public int InhibitorUnits;

    /// <summary>Ёмкость резервуара: по ней окно рисует заполненность.</summary>
    [AutoNetworkedField]
    public int ReservoirCapacity;

    /// <summary>Версия состояния, меняется после каждой обработанной команды.</summary>
    [AutoNetworkedField]
    public int Revision;

    /// <summary>Локализованный результат последней операции.</summary>
    [AutoNetworkedField]
    public string Feedback = "disease-microscope-feedback-ready";
}
