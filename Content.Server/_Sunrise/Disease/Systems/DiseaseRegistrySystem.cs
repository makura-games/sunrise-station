using System.Diagnostics.CodeAnalysis;
using Content.Server._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.GameTicking;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Заводит болезни раунда как сущности и выдаёт их по прототипу-шаблону.
/// </summary>
/// <remarks>
/// Прототип описывает рамку — класс возбудителя, пути передачи, форму стадий и возможные симптомы, —
/// а конкретный набор симптомов катается при создании болезни. Поэтому выученная картина
/// «эта болезнь — это кашель и жар» не работает: в следующем раунде тот же архетип
/// проявится чиханием и тошнотой. Выведенные под микроскопом штаммы создаются той же системой,
/// только их стадии задаёт вирусолог, а не бросок.
/// </remarks>
public sealed partial class DiseaseRegistrySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MetaDataSystem _metaData = default!;

    /// <summary>Болезни раунда, уже созданные по своим прототипам.</summary>
    private readonly Dictionary<ProtoId<DiseasePrototype>, EntityUid> _natural = new();

    /// <summary>Все живые болезни раунда, включая выведенные.</summary>
    private readonly List<EntityUid> _diseases = new();

    /// <summary>Уже выданные коды штаммов: два штамма не должны называться одинаково.</summary>
    private readonly HashSet<string> _codes = new();

    /// <summary>
    /// Штаммы раунда по их кодам.
    /// </summary>
    /// <remarks>
    /// По коду болезнь адресуют админские команды: имени прототипа не хватает, когда в раунде
    /// живут несколько штаммов одной болезни, и у выведенных штаммов прототипа нет вовсе.
    /// </remarks>
    private readonly Dictionary<string, EntityUid> _byCode = new();

    /// <summary>Буквы кода штамма: без похожих на цифры, чтобы код читался с бумаги.</summary>
    private const string CodeLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    [Dependency] private EntityQuery<DiseaseDefinitionComponent> _definitionQuery = default!;

    [SubscribeLocalEvent]
    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        foreach (var disease in _diseases)
        {
            if (!TerminatingOrDeleted(disease))
                QueueDel(disease);
        }

        _natural.Clear();
        _diseases.Clear();
        _codes.Clear();
        _byCode.Clear();
    }

    /// <summary>
    /// Возвращает болезнь раунда по её прототипу, создавая её при первом обращении.
    /// </summary>
    /// <returns>Null, если прототипа нет.</returns>
    public EntityUid? GetNatural(ProtoId<DiseasePrototype> template)
    {
        if (_natural.TryGetValue(template, out var existing) && !TerminatingOrDeleted(existing))
            return existing;

        if (!_proto.TryIndex(template, out var proto))
            return null;

        var disease = Create(proto);
        _natural[template] = disease;
        return disease;
    }

    /// <summary>
    /// Заводит новый штамм по прототипу.
    /// </summary>
    /// <remarks>
    /// В отличие от <see cref="GetNatural"/> каждый вызов создаёт отдельную болезнь со своим кодом
    /// и своим набором симптомов. Этим пользуются источники с собственной вспышкой: событие,
    /// артефакт, партия груза. Две вспышки одного прототипа за раунд должны лечиться
    /// двумя разными вакцинами, иначе вторая закрывается вакциной от первой.
    /// </remarks>
    public EntityUid? CreateStrain(ProtoId<DiseasePrototype> template)
    {
        return _proto.TryIndex(template, out var proto) ? Create(proto) : null;
    }

    /// <summary>
    /// Создаёт болезнь с заданным вектором развития: по пулу симптомов на стадию.
    /// </summary>
    /// <remarks>
    /// Так выглядит выведенный штамм: рамку он берёт у архетипа, а стадии получает ровно те,
    /// которые собрал вирусолог, поэтому болезнь развивается по заданному плану.
    /// </remarks>
    /// <param name="archetype">Архетип-рамка: иммунитет и скорость развития.</param>
    /// <param name="genome">Пулы симптомов по стадиям, начиная с первой после инкубации.</param>
    /// <param name="routes">Развитые пути передачи; по умолчанию только инъекции.</param>
    /// <param name="resistance">Резистентность исходной заготовки от нуля до единицы.</param>
    /// <returns>Null, если архетипа нет.</returns>
    public EntityUid? CreateDesigned(ProtoId<DiseasePrototype> archetype, IReadOnlyList<DiseaseStagePool> genome,
        DiseaseTransmissionRoute routes = DiseaseTransmissionRoute.Injection, float resistance = 0f)
    {
        if (genome.Count == 0 || !_proto.TryIndex(archetype, out var proto))
            return null;

        var disease = Create(proto, genome);
        var definition = Comp<DiseaseDefinitionComponent>(disease);
        definition.Designed = true;
        definition.Routes = routes;
        EnsureDropletSymptom(definition, proto);
        definition.Resistance = float.IsFinite(resistance) ? Math.Clamp(resistance, 0f, 1f) : 0f;
        return disease;
    }

    /// <summary>Данные болезни, если сущность ещё жива.</summary>
    public bool TryGetDefinition(EntityUid disease, out Entity<DiseaseDefinitionComponent> definition)
    {
        definition = default;
        if (TerminatingOrDeleted(disease) || !_definitionQuery.TryComp(disease, out var comp))
            return false;

        definition = (disease, comp);
        return true;
    }

    /// <summary>Данные болезни: чем она опасна и какие симптомы у этого штамма.</summary>
    public bool TryGetData(EntityUid disease, [NotNullWhen(true)] out DiseaseDefinitionComponent? data)
    {
        data = null;
        if (TerminatingOrDeleted(disease) || !_definitionQuery.TryComp(disease, out var comp))
            return false;

        data = comp;
        return true;
    }

    /// <summary>
    /// Находит штамм раунда по коду с этикетки. Регистр не важен.
    /// </summary>
    /// <returns>False, если такого кода в раунде не было или штамм уже удалён.</returns>
    public bool TryGetByCode(string code, out EntityUid disease)
    {
        disease = default;
        if (!_byCode.TryGetValue(code.ToUpperInvariant(), out var existing) || TerminatingOrDeleted(existing))
            return false;

        disease = existing;
        return true;
    }

    /// <summary>Жив ли ещё этот штамм: сущность не удалена и это действительно болезнь.</summary>
    public bool StrainExists(EntityUid disease)
    {
        return !TerminatingOrDeleted(disease) && _definitionQuery.HasComp(disease);
    }

    /// <summary>Все болезни, заведённые в этом раунде.</summary>
    public IReadOnlyList<EntityUid> GetDiseases()
    {
        return _diseases;
    }

    /// <summary>Пул симптомов стадии у этого штамма.</summary>
    public IReadOnlyList<DiseaseSymptomEntry> GetStagePool(EntityUid disease, int stage)
    {
        if (!TryGetDefinition(disease, out var definition) || stage < 0 || stage >= definition.Comp.Stages.Count)
            return Array.Empty<DiseaseSymptomEntry>();

        return definition.Comp.Stages[stage].SymptomPool;
    }

    /// <summary>Все симптомы, которые штамм способен вызвать на любой стадии.</summary>
    public IReadOnlyCollection<ProtoId<DiseaseSymptomPrototype>> GetSymptoms(EntityUid disease)
    {
        return TryGetDefinition(disease, out var definition)
            ? definition.Comp.Symptoms
            : Array.Empty<ProtoId<DiseaseSymptomPrototype>>();
    }

    /// <summary>
    /// Создаёт сущность болезни по прототипу, прокатывая штамм или принимая готовый вектор развития.
    /// </summary>
    private EntityUid Create(DiseasePrototype proto, IReadOnlyList<DiseaseStagePool>? genome = null)
    {
        // Болезнь живёт в нигде: она не предмет на станции, а запись о том, чем болеют люди.
        var disease = Spawn(null, MapCoordinates.Nullspace);
        var comp = EnsureComp<DiseaseDefinitionComponent>(disease);

        comp.Template = proto.ID;
        comp.StrainCode = CreateCode();
        _byCode[comp.StrainCode] = disease;
        comp.Name = proto.Name;
        comp.Description = proto.Description;
        comp.Syndrome = proto.Syndrome;
        comp.Classification = proto.Classification;
        comp.TreatmentHints = new List<LocId>(proto.TreatmentHints);
        comp.ProcessInterval = proto.ProcessInterval;
        comp.Regression = proto.Regression;
        comp.Routes = proto.Routes;
        comp.Whitelist = proto.Whitelist;
        comp.Blacklist = proto.Blacklist;
        comp.CureThreshold = proto.CureThreshold;
        comp.SleepCureTime = proto.SleepCureTime;
        comp.SuppressionPausesProgression = proto.SuppressionPausesProgression;
        comp.SuppressedSpreadMultiplier = proto.SuppressedSpreadMultiplier;
        comp.Immunity = proto.Immunity;
        comp.Stages = genome == null ? RollStages(proto) : BuildStages(proto, genome);

        FillSymptoms(comp);
        if (genome == null && comp.Symptoms.Count < proto.MinStrainSymptoms)
            TopUp(proto, comp);

        // У выведенного штамма пути проставляются после сборки, гарантию даст CreateDesigned.
        if (genome == null)
            EnsureDropletSymptom(comp, proto);

        _diseases.Add(disease);
        _metaData.SetEntityName(disease, $"disease {proto.ID}");
        return disease;
    }

    /// <summary>
    /// Выдаёт свободный код штамма: по нему станция связывает образцы, отчёты и вакцины.
    /// </summary>
    private string CreateCode()
    {
        string code;
        do
        {
            code = $"{CodeLetters[_random.Next(CodeLetters.Length)]}{CodeLetters[_random.Next(CodeLetters.Length)]}-{_random.Next(100, 1000)}";
        }
        while (!_codes.Add(code));

        return code;
    }

    /// <summary>
    /// Катает штамм: по каждой стадии оставляет из пула столько симптомов, сколько велит архетип.
    /// </summary>
    private List<DiseaseStage> RollStages(DiseasePrototype proto)
    {
        var stages = new List<DiseaseStage>(proto.Stages.Count);
        foreach (var stage in proto.Stages)
        {
            // Полезные симптомы выводятся только под микроскопом: в природной болезни их не бывает,
            // даже если прототип по ошибке их перечислил.
            var source = stage.SymptomPool.FindAll(entry => !IsBeneficial(entry.Symptom));

            // Болезни с выключенным броском работают ровно так, как написаны в прототипе.
            var rollCount = proto.RollStrain ? stage.RollCount : 0;
            var pool = rollCount <= 0 || rollCount >= source.Count
                ? source
                : PickEntries(source, rollCount);

            stages.Add(CopyStage(stage, pool));
        }

        return stages;
    }

    /// <summary>
    /// Собирает стадии выведенного штамма: нулевая остаётся инкубацией, дальше идут собранные пулы.
    /// </summary>
    private static List<DiseaseStage> BuildStages(DiseasePrototype proto, IReadOnlyList<DiseaseStagePool> genome)
    {
        var stages = new List<DiseaseStage>(genome.Count + 1);
        stages.Add(CopyStage(proto.Stages[0], new List<DiseaseSymptomEntry>()));

        for (var i = 0; i < genome.Count; i++)
        {
            // Форма стадии берётся у архетипа, а если собрали длиннее — повторяется последняя.
            var shape = proto.Stages[Math.Min(i + 1, proto.Stages.Count - 1)];
            var pool = new List<DiseaseSymptomEntry>(genome[i].Symptoms.Count);
            foreach (var entry in genome[i].Symptoms)
                pool.Add(new DiseaseSymptomEntry { Symptom = entry.Symptom, Weight = entry.Weight });

            // Вирусолог вырастил каждый симптом стадии вручную, поэтому проявляются все, без жребия.
            var stage = CopyStage(shape, pool);
            stage.MinSymptoms = pool.Count;
            stage.MaxSymptoms = pool.Count;
            stages.Add(stage);
        }

        return stages;
    }

    private static DiseaseStage CopyStage(DiseaseStage shape, List<DiseaseSymptomEntry> pool)
    {
        return new DiseaseStage
        {
            Advance = shape.Advance,
            SymptomPool = pool,
            RollCount = 0,
            MinSymptoms = shape.MinSymptoms,
            MaxSymptoms = shape.MaxSymptoms,
            EnterEffects = shape.EnterEffects,
            SpreadMultiplier = shape.SpreadMultiplier,
            ContactSpreadChance = shape.ContactSpreadChance,
        };
    }

    /// <summary>
    /// Берёт из пула заданное число разных симптомов, тем вероятнее, чем больше вес записи.
    /// </summary>
    private List<DiseaseSymptomEntry> PickEntries(List<DiseaseSymptomEntry> pool, int count)
    {
        var candidates = new List<DiseaseSymptomEntry>(pool);
        var picked = new List<DiseaseSymptomEntry>(count);
        var totalWeight = 0f;

        foreach (var entry in candidates)
            totalWeight += Math.Max(entry.Weight, 0f);

        while (picked.Count < count && candidates.Count > 0)
        {
            var index = PickWeightedIndex(candidates, totalWeight);
            totalWeight -= Math.Max(candidates[index].Weight, 0f);
            picked.Add(candidates[index]);
            candidates.RemoveAt(index);
        }

        return picked;
    }

    private int PickWeightedIndex(List<DiseaseSymptomEntry> candidates, float totalWeight)
    {
        if (totalWeight <= 0f)
            return _random.Next(candidates.Count);

        var roll = _random.NextFloat() * totalWeight;
        for (var i = 0; i < candidates.Count; i++)
        {
            roll -= Math.Max(candidates[i].Weight, 0f);
            if (roll <= 0f)
                return i;
        }

        return candidates.Count - 1;
    }

    /// <summary>
    /// Добирает симптомы из непопавших в бросок, пока штамм не станет пригодным для загадки.
    /// </summary>
    private void TopUp(DiseasePrototype proto, DiseaseDefinitionComponent comp)
    {
        for (var i = 0; i < proto.Stages.Count && i < comp.Stages.Count; i++)
        {
            foreach (var entry in proto.Stages[i].SymptomPool)
            {
                if (comp.Symptoms.Count >= proto.MinStrainSymptoms)
                    return;

                if (comp.Symptoms.Contains(entry.Symptom) || IsBeneficial(entry.Symptom))
                    continue;

                comp.Stages[i].SymptomPool.Add(entry);
                comp.Symptoms.Add(entry.Symptom);
            }
        }
    }

    /// <summary>
    /// Даёт штамму с воздушным путём симптом, от которого разлетаются капли.
    /// </summary>
    /// <remarks>
    /// Берётся самая ранняя стадия, где штамму уже достался такой симптом. Если не досталось,
    /// в первую стадию с симптомами кладётся вариант из пула архетипа, а если нет и его — кашель.
    /// Выведенный штамм проявляет все симптомы стадии, поэтому добавленный кашель проявится у каждого носителя.
    /// </remarks>
    private void EnsureDropletSymptom(DiseaseDefinitionComponent comp, DiseasePrototype proto)
    {
        comp.DropletSymptom = null;
        comp.DropletStage = -1;
        if ((comp.Routes & DiseaseTransmissionRoute.Droplet) == 0)
            return;

        var first = -1;
        for (var i = 0; i < comp.Stages.Count; i++)
        {
            var pool = comp.Stages[i].SymptomPool;
            if (pool.Count == 0)
                continue;

            if (first < 0)
                first = i;

            foreach (var entry in pool)
            {
                if (!IsDroplet(entry.Symptom))
                    continue;

                comp.DropletSymptom = entry.Symptom;
                comp.DropletStage = i;
                return;
            }
        }

        if (first < 0)
            return;

        var symptom = proto.DropletFallbackSymptom;
        var weight = 1f;
        if (!comp.Designed && first < proto.Stages.Count)
        {
            foreach (var entry in proto.Stages[first].SymptomPool)
            {
                if (!IsDroplet(entry.Symptom))
                    continue;

                symptom = entry.Symptom;
                weight = entry.Weight;
                break;
            }
        }

        var stage = comp.Stages[first];
        stage.SymptomPool.Add(new DiseaseSymptomEntry { Symptom = symptom, Weight = weight });
        if (comp.Designed)
        {
            stage.MinSymptoms++;
            stage.MaxSymptoms++;
        }

        comp.Symptoms.Add(symptom);
        comp.DropletSymptom = symptom;
        comp.DropletStage = first;
    }

    private bool IsDroplet(ProtoId<DiseaseSymptomPrototype> symptom)
    {
        return _proto.TryIndex(symptom, out var proto) && proto.Spread != null;
    }

    private bool IsBeneficial(ProtoId<DiseaseSymptomPrototype> symptom)
    {
        return _proto.TryIndex(symptom, out var proto) && proto.Beneficial;
    }

    /// <summary>
    /// Может ли штамм вызвать хоть один полезный симптом.
    /// </summary>
    /// <remarks>
    /// Без взлома микроскопа такой штамм остаётся уколом, а авария сосуда не даёт ему поблажки на путь передачи.
    /// </remarks>
    public bool IsBeneficial(EntityUid disease)
    {
        return TryGetData(disease, out var data) && HasBeneficial(data);
    }

    private bool HasBeneficial(DiseaseDefinitionComponent definition)
    {
        foreach (var symptom in definition.Symptoms)
        {
            if (IsBeneficial(symptom))
                return true;
        }

        return false;
    }

    private static void FillSymptoms(DiseaseDefinitionComponent comp)
    {
        comp.Symptoms.Clear();
        foreach (var stage in comp.Stages)
        {
            foreach (var entry in stage.SymptomPool)
                comp.Symptoms.Add(entry.Symptom);
        }
    }
}
