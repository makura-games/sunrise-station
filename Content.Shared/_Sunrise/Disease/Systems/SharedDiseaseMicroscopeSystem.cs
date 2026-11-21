using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Microscope;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Emag.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Disease.Systems;

/// <summary>
/// Предсказываемые операции микроскопа: ход, прогноз, засев поля, партия за путь передачи и взлом.
/// Клиент сразу меняет состояние машины, а окно перерисовывается из состояния компонента.
/// </summary>
/// <remarks>
/// Все предсказываемые операции детерминированы по реплицированному состоянию: ответ конкурента
/// зависит от поля, профиля и номера хода, а посев — от сущности машины и счётчика партий.
/// Устаревшие нажатия отсекают номер хода и счётчик партий, а не ревизия окна: ревизию сервер
/// двигает и сам, предсказать её нельзя. Завершение, продвижение и выпуск пишут в геном заготовки,
/// которого у клиента нет, и остаются за сервером. Что в слоте та же заготовка, сервер проверяет
/// в <see cref="ValidateSample"/>.
/// </remarks>
public abstract partial class SharedDiseaseMicroscopeSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    protected const string FeedbackPrefix = "disease-microscope-feedback-";

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<DiseaseMicroscopeComponent>(DiseaseMicroscopeUiKey.Key, subs =>
        {
            subs.Event<DiseaseMicroscopeApplyMessage>(OnApply);
            subs.Event<DiseaseMicroscopeForecastMessage>(OnForecast);
            subs.Event<DiseaseMicroscopePrepareMessage>(OnPrepare);
            subs.Event<DiseaseMicroscopeTransmissionMessage>(OnTransmission);
        });
    }

    /// <summary>
    /// Взлом снимает блокировку селекции путей передачи.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnEmagged(Entity<DiseaseMicroscopeComponent> ent, ref GotEmaggedEvent args)
    {
        if ((args.Type & EmagType.Interaction) == 0 || ent.Comp.TransmissionUnlocked)
            return;

        ent.Comp.TransmissionUnlocked = true;
        args.Handled = true;
        SetFeedback(ent, "transmission-unlocked");
    }

    private void OnApply(Entity<DiseaseMicroscopeComponent> ent, ref DiseaseMicroscopeApplyMessage args)
    {
        TryApply(ent, args.Tool, args.X, args.Y, args.Turn, args.Actor);
    }

    private void OnForecast(Entity<DiseaseMicroscopeComponent> ent, ref DiseaseMicroscopeForecastMessage args)
    {
        TryForecast(ent, args.Turn, args.Actor);
    }

    private void OnPrepare(Entity<DiseaseMicroscopeComponent> ent, ref DiseaseMicroscopePrepareMessage args)
    {
        TryPrepare(ent, args.Symptom, args.MatchCounter, args.Actor);
    }

    private void OnTransmission(Entity<DiseaseMicroscopeComponent> ent, ref DiseaseMicroscopeTransmissionMessage args)
    {
        TryPrepareTransmission(ent, args.Route, args.MatchCounter, args.Actor);
    }

    #region Ход

    /// <summary>
    /// Выполняет ход игрока и сразу разыгрывает ответ конкурента.
    /// </summary>
    /// <param name="turn">Номер хода, на котором игрок видел поле. Устаревший ход отклоняется.</param>
    /// <returns>True, если ход принят.</returns>
    public bool TryApply(
        Entity<DiseaseMicroscopeComponent> ent,
        DiseaseMicroscopeTool tool,
        int x,
        int y,
        int turn,
        EntityUid? user = null)
    {
        if (!CanApply(ent, tool, x, y, turn, out var feedback))
        {
            SetFeedback(ent, feedback);
            return false;
        }

        var comp = ent.Comp;
        var nutrient = tool == DiseaseMicroscopeTool.Nutrient;
        Consume(ent, nutrient, GetCost(comp, tool));

        var next = DiseaseCultureBoard.Apply(comp.Cells, tool, x, y, comp.Profile);
        comp.Turn = unchecked(comp.Turn + 1);

        // Конкурент отвечает на каждый ход игрока, поэтому партия идёт по очереди, а не в одни ворота.
        comp.Cells = DiseaseCultureOpponent.Respond(next, comp.Profile, comp.Turn, out var moves);
        comp.OpponentMoves = moves;

        // Окрашенный мазок показывает ровно один ответ: после хода культура уже другая.
        comp.ForecastActive = false;
        Complete(ent, nutrient ? "grown" : "inhibited", nutrient ? comp.NutrientSound : comp.InhibitorSound, user);
        return true;
    }

    /// <summary>
    /// Проверяет питание, заготовку, номер хода, границы поля, полезность хода и запас реагента.
    /// </summary>
    public bool CanApply(
        Entity<DiseaseMicroscopeComponent> ent,
        DiseaseMicroscopeTool tool,
        int x,
        int y,
        int turn,
        out string feedback)
    {
        var comp = ent.Comp;
        if (!CanOperate(ent, out feedback))
            return false;

        feedback = "stale";
        if (turn != comp.Turn)
            return false;

        feedback = "no-change";
        if (!Enum.IsDefined(tool) || !DiseaseCultureBoard.IsInside(x, y) || !IsMatchValid(comp))
            return false;

        var next = DiseaseCultureBoard.Apply(comp.Cells, tool, x, y, comp.Profile);
        if (SameCells(comp.Cells, next))
            return false;

        var nutrient = tool == DiseaseMicroscopeTool.Nutrient;
        feedback = nutrient ? "no-nutrient" : "no-inhibitor";
        return (nutrient ? comp.NutrientUnits : comp.InhibitorUnits) >= GetCost(comp, tool);
    }

    #endregion

    #region Прогноз

    /// <summary>
    /// Окрашивает мазок: до следующего хода окно предсказывает ответ конкурента на наведённую каплю.
    /// </summary>
    /// <remarks>
    /// Ответ конкурента детерминирован по полю и номеру хода, поэтому прогноз считает клиент,
    /// а машина ведёт только запас прогнозов и списывает за них ингибитор.
    /// </remarks>
    public bool TryForecast(Entity<DiseaseMicroscopeComponent> ent, int turn, EntityUid? user = null)
    {
        if (!CanForecast(ent, turn, out var feedback))
        {
            SetFeedback(ent, feedback);
            return false;
        }

        var comp = ent.Comp;
        Consume(ent, false, comp.Profile.InhibitorCost);
        comp.ForecastsLeft--;
        comp.ForecastActive = true;
        Complete(ent, "forecast", comp.SuccessSound, user);
        return true;
    }

    /// <summary>Проверяет, идёт ли партия, остались ли прогнозы и хватает ли ингибитора.</summary>
    public bool CanForecast(Entity<DiseaseMicroscopeComponent> ent, int turn, out string feedback)
    {
        var comp = ent.Comp;
        if (!CanOperate(ent, out feedback))
            return false;

        feedback = "stale";
        if (turn != comp.Turn)
            return false;

        feedback = "no-match";
        if (!IsMatchValid(comp))
            return false;

        feedback = "forecast-active";
        if (comp.ForecastActive)
            return false;

        feedback = "no-forecasts";
        if (comp.ForecastsLeft <= 0)
            return false;

        feedback = "no-inhibitor";
        return comp.InhibitorUnits >= comp.Profile.InhibitorCost;
    }

    #endregion

    #region Засев

    /// <summary>Засевает поле под выбранный симптом стадии и ставит против него колонию-конкурента.</summary>
    /// <param name="matchCounter">Счётчик партий, при котором игрок нажал засев.</param>
    public bool TryPrepare(Entity<DiseaseMicroscopeComponent> ent, int symptom, int matchCounter, EntityUid? user = null)
    {
        if (!CanPrepare(ent, symptom, matchCounter, out var feedback))
        {
            SetFeedback(ent, feedback);
            return false;
        }

        var comp = ent.Comp;
        var target = comp.StageOptions[symptom];
        var random = NextMatchRandom(ent);

        // Конкурента выбирает жребий, и развить его на этой стадии уже не выйдет.
        var rivals = new List<ProtoId<DiseaseSymptomPrototype>>(comp.StageOptions);
        rivals.Remove(target);

        // При усилении свободных вариантов может не остаться: тогда против цели идёт прежняя примесь.
        var rival = rivals.Count > 0 ? rivals[random.Next(rivals.Count)] : comp.RivalSymptom ?? target;

        comp.BonusMatch = comp.StageOpen;
        comp.TargetSymptom = target;
        comp.RivalSymptom = rival;
        comp.Turn = 0;
        comp.OpponentMoves = new();
        comp.ForecastsLeft = Math.Max(0, comp.ForecastsPerMatch);
        comp.ForecastActive = false;
        comp.Cells = DiseaseCultureBoard.Create(random.Next(), comp.Profile);

        Consume(ent, true, comp.PrepareCost);
        Complete(ent, "prepared", comp.SuccessSound, user);
        return true;
    }

    /// <summary>Проверяет выбранную цель, стадию и запас среды, ничего не меняя.</summary>
    public bool CanPrepare(Entity<DiseaseMicroscopeComponent> ent, int symptom, int matchCounter, out string feedback)
    {
        var comp = ent.Comp;
        if (!CanOperate(ent, out feedback))
            return false;

        // Партия уже засеяна этим или более ранним нажатием.
        feedback = "stale";
        if (matchCounter != comp.MatchCounter || IsMatchValid(comp))
            return false;

        feedback = "genome-complete";
        if (comp.TargetRoute != DiseaseTransmissionRoute.None || !comp.Profile.IsValid ||
            !comp.StageOpen && comp.Stage >= comp.MaxStage)
        {
            return false;
        }

        feedback = "invalid-sample";
        if (symptom < 0 || symptom >= comp.StageOptions.Count)
            return false;

        // Новой стадии нужны две колонии: цель и конкурент. Усилению хватает одной цели.
        feedback = "no-options";
        if (!comp.StageOpen && comp.StageOptions.Count < 2)
            return false;

        feedback = "no-nutrient";
        return comp.NutrientUnits >= comp.PrepareCost;
    }

    /// <summary>Засевает отдельную партию за дополнительный путь передачи.</summary>
    public bool TryPrepareTransmission(
        Entity<DiseaseMicroscopeComponent> ent,
        DiseaseTransmissionRoute route,
        int matchCounter,
        EntityUid? user = null)
    {
        if (!CanPrepareTransmission(ent, route, matchCounter, out var feedback))
        {
            SetFeedback(ent, feedback);
            return false;
        }

        var comp = ent.Comp;
        var random = NextMatchRandom(ent);
        ResetMatch(comp);
        comp.TargetRoute = route;

        // Развитие передачи не отнимает стадию симптомов и требует отдельной партии.
        comp.Profile = DiseaseCultureProfile.FromStage(2, comp.SampleResistance);
        comp.Cells = DiseaseCultureBoard.Create(random.Next(), comp.Profile);
        comp.ForecastsLeft = Math.Max(0, comp.ForecastsPerMatch);

        Consume(ent, true, comp.PrepareCost);
        Complete(ent, "prepared", comp.SuccessSound, user);
        return true;
    }

    /// <summary>Проверяет блокировку, путь, идущую партию и запас среды.</summary>
    public bool CanPrepareTransmission(
        Entity<DiseaseMicroscopeComponent> ent,
        DiseaseTransmissionRoute route,
        int matchCounter,
        out string feedback)
    {
        var comp = ent.Comp;
        if (!CanOperate(ent, out feedback))
            return false;

        feedback = "stale";
        if (matchCounter != comp.MatchCounter)
            return false;

        feedback = "transmission-locked";
        if (!comp.TransmissionUnlocked)
            return false;

        feedback = "no-options";
        if (route is not (DiseaseTransmissionRoute.Contact or DiseaseTransmissionRoute.Ingestion or DiseaseTransmissionRoute.Droplet) ||
            (comp.Routes & route) != 0 || IsMatchValid(comp))
        {
            return false;
        }

        feedback = "no-nutrient";
        return comp.NutrientUnits >= comp.PrepareCost;
    }

    #endregion

    /// <summary>
    /// Серверная проверка, что в слоте та же заготовка, на которой идёт партия.
    /// </summary>
    /// <remarks>
    /// Данные заготовки клиенту не отдаются, поэтому клиент доверяет реплицированному состоянию машины.
    /// </remarks>
    protected virtual bool ValidateSample(Entity<DiseaseMicroscopeComponent> ent)
    {
        return true;
    }

    /// <summary>
    /// Перерисовывает открытое окно машины. На клиенте — после предсказанной операции.
    /// </summary>
    protected virtual void UpdateUi(Entity<DiseaseMicroscopeComponent> ent)
    {
    }

    /// <summary>Идёт ли партия: есть поле нужного размера, цель и конкурент.</summary>
    protected static bool IsMatchValid(DiseaseMicroscopeComponent comp)
    {
        if (comp.Cells.Length != DiseaseCultureBoard.CellCount || !comp.Profile.IsValid ||
            comp.TargetRoute == DiseaseTransmissionRoute.None && (comp.TargetSymptom == null || comp.RivalSymptom == null))
        {
            return false;
        }

        for (var i = 0; i < comp.Cells.Length; i++)
        {
            var cell = comp.Cells[i];
            if (cell == 0)
                continue;

            if (cell != DiseaseCultureBoard.Target && cell != DiseaseCultureBoard.Opponent &&
                cell != DiseaseCultureBoard.Sterile)
            {
                return false;
            }

            if (!DiseaseCultureBoard.IsInside(i % DiseaseCultureBoard.Size, i / DiseaseCultureBoard.Size))
                return false;
        }

        return true;
    }

    /// <summary>Снимает незаконченную партию, не трогая уже развитый геном.</summary>
    protected static void ResetMatch(DiseaseMicroscopeComponent comp)
    {
        comp.TargetRoute = DiseaseTransmissionRoute.None;
        comp.Cells = [];
        comp.OpponentMoves = new();
        comp.TargetSymptom = null;
        comp.RivalSymptom = null;
        comp.BonusMatch = false;
        comp.Turn = 0;
        comp.ForecastsLeft = 0;
        comp.ForecastActive = false;
    }

    /// <summary>Сколько реагента снимает одно применение инструмента на текущей стадии.</summary>
    protected static int GetCost(DiseaseMicroscopeComponent comp, DiseaseMicroscopeTool tool)
    {
        return tool == DiseaseMicroscopeTool.Nutrient ? comp.Profile.NutrientCost : comp.Profile.InhibitorCost;
    }

    /// <summary>Копирует вектор развития, чтобы состояние окна не делило списки с образцом.</summary>
    protected static List<DiseaseStagePool> CopyGenome(IReadOnlyList<DiseaseStagePool> genome)
    {
        var copy = new List<DiseaseStagePool>(genome.Count);
        foreach (var stage in genome)
        {
            // Счётчик развитых симптомов копируется вместе с пулом: по нему считается запас усилений.
            var pool = new DiseaseStagePool { Developed = stage.Developed };
            foreach (var entry in stage.Symptoms)
                pool.Symptoms.Add(new DiseaseSymptomEntry { Symptom = entry.Symptom, Weight = entry.Weight });

            copy.Add(pool);
        }

        return copy;
    }

    /// <summary>Совпадают ли списки симптомов по составу и порядку.</summary>
    protected static bool SameSymptoms(
        IReadOnlyList<ProtoId<DiseaseSymptomPrototype>> first,
        IReadOnlyList<ProtoId<DiseaseSymptomPrototype>> second)
    {
        if (first.Count != second.Count)
            return false;

        for (var i = 0; i < first.Count; i++)
        {
            if (first[i] != second[i])
                return false;
        }

        return true;
    }

    /// <summary>Совпадают ли векторы развития по стадиям, симптомам и счётчику развитых.</summary>
    protected static bool SameGenome(IReadOnlyList<DiseaseStagePool> first, IReadOnlyList<DiseaseStagePool> second)
    {
        if (first.Count != second.Count)
            return false;

        for (var i = 0; i < first.Count; i++)
        {
            if (first[i].Symptoms.Count != second[i].Symptoms.Count || first[i].Developed != second[i].Developed)
                return false;

            for (var j = 0; j < first[i].Symptoms.Count; j++)
            {
                if (first[i].Symptoms[j].Symptom != second[i].Symptoms[j].Symptom ||
                    !first[i].Symptoms[j].Weight.Equals(second[i].Symptoms[j].Weight))
                {
                    return false;
                }
            }
        }

        return true;
    }

    protected static bool SameCells(IReadOnlyList<byte> first, IReadOnlyList<byte> second)
    {
        if (first.Count != second.Count)
            return false;

        for (var i = 0; i < first.Count; i++)
        {
            if (first[i] != second[i])
                return false;
        }

        return true;
    }

    /// <summary>Общие условия любой операции: питание и та же заготовка в слоте.</summary>
    private bool CanOperate(Entity<DiseaseMicroscopeComponent> ent, out string feedback)
    {
        feedback = "unpowered";
        if (!ent.Comp.Powered)
            return false;

        feedback = "invalid-sample";
        return ent.Comp.HasSample && ValidateSample(ent);
    }

    /// <summary>
    /// Генератор посева новой партии. Сид одинаков на клиенте и сервере: сетевой номер машины
    /// и счётчик партий, который тут же сдвигается.
    /// </summary>
    private System.Random NextMatchRandom(Entity<DiseaseMicroscopeComponent> ent)
    {
        var seed = unchecked(GetNetEntity(ent.Owner).Id * 73856093 ^ ent.Comp.MatchCounter * 19349663);
        ent.Comp.MatchCounter = unchecked(ent.Comp.MatchCounter + 1);
        return new System.Random(seed);
    }

    /// <summary>
    /// Списывает реагент из резервуара вместе с его зеркалом в компоненте.
    /// </summary>
    /// <remarks>
    /// Зеркало обновляется до списания: серверный пересчёт по изменению раствора тогда
    /// не видит разницы и не сдвигает ревизию окна, которую предсказать нельзя.
    /// </remarks>
    private void Consume(Entity<DiseaseMicroscopeComponent> ent, bool nutrient, int amount)
    {
        var comp = ent.Comp;
        if (nutrient)
            comp.NutrientUnits -= amount;
        else
            comp.InhibitorUnits -= amount;

        var reagent = nutrient ? comp.NutrientReagent : comp.InhibitorReagent;
        if (_solution.TryGetSolution(ent.Owner, comp.SolutionName, out var solution, out _))
            _solution.RemoveReagent(solution.Value, new ReagentId(reagent.Id, null), FixedPoint2.New(amount));
    }

    /// <summary>Завершает принятую операцию: строка результата, звук для всех и перерисовка окна.</summary>
    private void Complete(Entity<DiseaseMicroscopeComponent> ent, string feedback, SoundSpecifier sound, EntityUid? user)
    {
        ent.Comp.Feedback = FeedbackPrefix + feedback;
        Dirty(ent);
        _audio.PlayPredicted(sound, ent.Owner, user);
        UpdateUi(ent);
    }

    private void SetFeedback(Entity<DiseaseMicroscopeComponent> ent, string feedback)
    {
        ent.Comp.Feedback = FeedbackPrefix + feedback;
        Dirty(ent);
        UpdateUi(ent);
    }
}
