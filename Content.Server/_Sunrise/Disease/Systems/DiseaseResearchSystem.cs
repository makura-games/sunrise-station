using Content.Server._Sunrise.Disease.Components;
using Content.Server._Sunrise.Disease.Events;
using Content.Shared._Sunrise.Disease;
using Content.Shared._Sunrise.Disease.Analysis;
using Content.Shared._Sunrise.Disease.Database;
using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Ведёт лабораторное дело по каждому штамму: собранные данные анализов, прогресс исследования
/// и пробные партии вакцины с заявленными свойствами.
/// </summary>
/// <remarks>
/// Партии генерируются заново каждый раунд, поэтому ответ нельзя выучить заранее.
/// Дело общее для станции: любой вакцинатор и любая консоль показывают уже добытые данные.
/// Размер загадки берётся из профиля сложности, а он — из силы болезни.
/// </remarks>
public sealed partial class DiseaseResearchSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DiseaseAnalysisSystem _analysis = default!;
    [Dependency] private DiseaseStrengthSystem _strength = default!;
    [Dependency] private DiseaseRegistrySystem _registry = default!;

    [Dependency] private EntityQuery<DiseaseResearchCaseComponent> _caseQuery = default!;

    #region Знания

    /// <summary>
    /// Записывает в дело то, что показал анализ образца: замеченные симптомы и, при точном анализе, класс возбудителя.
    /// Пробные партии строятся именно по этим данным, поэтому чем больше обследовано пациентов,
    /// тем больше партий можно отсеять, не тратя образцы.
    /// </summary>
    public void RevealFacts(
        EntityUid disease,
        IReadOnlyList<ProtoId<DiseaseSymptomPrototype>> symptoms,
        bool revealClassification)
    {
        if (!TryGetCase(disease, out var diseaseCase))
            return;

        foreach (var symptom in symptoms)
        {
            diseaseCase.ObservedSymptoms.Add(symptom);
        }

        if (revealClassification)
            diseaseCase.ClassificationKnown = true;

        MarkChanged();
    }

    /// <summary>
    /// Раскрывает один ещё не замеченный симптом штамма. Так работает анализ культуры:
    /// живого носителя у неё нет, зато лаборатория видит, на что штамм способен.
    /// </summary>
    /// <returns>True, если симптом удалось раскрыть.</returns>
    public bool TryRevealRandomSymptom(EntityUid disease)
    {
        if (!TryGetCase(disease, out var diseaseCase) || !_registry.TryGetData(disease, out var proto))
            return false;

        var hidden = new List<ProtoId<DiseaseSymptomPrototype>>();
        foreach (var symptom in _registry.GetSymptoms(disease))
        {
            if (!diseaseCase.ObservedSymptoms.Contains(symptom))
                hidden.Add(symptom);
        }

        if (hidden.Count == 0)
            return false;

        diseaseCase.ObservedSymptoms.Add(_random.Pick(hidden));
        MarkChanged();
        return true;
    }

    /// <summary>
    /// Добавляет очки исследования штамма. Очки снимают замки со свойств пробных партий.
    /// </summary>
    public void AddResearch(EntityUid disease, int points)
    {
        if (points <= 0 || !TryGetCase(disease, out var diseaseCase))
            return;

        diseaseCase.Research += points;
        MarkChanged();
    }

    /// <summary>
    /// Накопленные очки исследования штамма.
    /// </summary>
    public int GetResearch(EntityUid disease)
    {
        return _caseQuery.TryComp(disease, out var diseaseCase) ? diseaseCase.Research : 0;
    }

    /// <summary>
    /// Сколько очков нужно, чтобы расшифровать все свойства партий этого штамма.
    /// </summary>
    public int GetResearchGoal(EntityUid disease)
    {
        if (_strength.GetDifficulty(disease) is not { } difficulty)
            return 0;

        return difficulty.LockedClaims * difficulty.ResearchPerUnlock;
    }

    /// <summary>
    /// Штаммы, о которых станция хоть что-то знает, в порядке их появления в раунде.
    /// </summary>
    /// <remarks>
    /// Порядок берётся у реестра: тот только дописывает штаммы, поэтому индексы в интерфейсе
    /// консоли остаются устойчивыми, пока дело не закрыли.
    /// </remarks>
    public List<EntityUid> GetKnownDiseases()
    {
        var known = new List<EntityUid>();
        foreach (var disease in _registry.GetDiseases())
        {
            if (_caseQuery.HasComp(disease))
                known.Add(disease);
        }

        return known;
    }

    /// <summary>
    /// Собирает запись базы данных по штамму.
    /// </summary>
    /// <returns>Null, если о штамме ещё ничего не известно.</returns>
    public DiseaseDatabaseEntry? GetDatabaseEntry(EntityUid disease)
    {
        if (!_caseQuery.TryComp(disease, out var diseaseCase) || !_registry.TryGetData(disease, out var proto))
            return null;

        var symptoms = new List<string>(diseaseCase.ObservedSymptoms.Count);
        foreach (var symptomId in diseaseCase.ObservedSymptoms)
        {
            if (_proto.TryIndex(symptomId, out var symptom))
                symptoms.Add(symptom.Name.Id);
        }

        var classification = diseaseCase.ClassificationKnown
            ? DiseaseLocalization.GetClassificationLocId(proto.Classification).Id
            : null;

        return new DiseaseDatabaseEntry(
            _analysis.GetStrainCode(disease),
            proto.Syndrome.Id,
            classification,
            symptoms,
            diseaseCase.Research,
            GetResearchGoal(disease),
            diseaseCase.Solved);
    }

    #endregion

    #region Партии

    /// <summary>
    /// Собирает состояние дела для интерфейса вакцинатора.
    /// </summary>
    public DiseaseResearchState? GetResearchState(EntityUid disease)
    {
        if (!_registry.StrainExists(disease))
            return null;

        var strainCode = _analysis.GetStrainCode(disease);

        // Дело открывается только анализами: без них машине нечего показывать, кроме кода штамма.
        if (!_caseQuery.TryComp(disease, out var diseaseCase))
        {
            return new DiseaseResearchState(
                strainCode,
                new List<DiseaseVaccineCandidate>(),
                null,
                0,
                GetResearchGoal(disease),
                hasData: false,
                missingFacts: 0);
        }

        EnsureCandidates(disease, diseaseCase);
        FillSlots(disease, diseaseCase);

        var candidates = new List<DiseaseVaccineCandidate>(diseaseCase.Candidates.Count);
        foreach (var candidate in diseaseCase.Candidates)
        {
            var lines = new List<DiseaseClaimLine>(candidate.Slots.Count);
            foreach (var slot in candidate.Slots)
            {
                string? text = null;
                if (slot.Claim is { } claim && TryGetClaimText(claim, out var claimText))
                    text = claimText;

                lines.Add(new DiseaseClaimLine(text, slot.RevealAt));
            }

            candidates.Add(new DiseaseVaccineCandidate(candidate.Code, lines, candidate.RuledOut));
        }

        var solvedCode = diseaseCase.Solved && diseaseCase.SolutionIndex >= 0
            ? diseaseCase.Candidates[diseaseCase.SolutionIndex].Code
            : null;

        return new DiseaseResearchState(
            strainCode,
            candidates,
            solvedCode,
            diseaseCase.Research,
            GetResearchGoal(disease),
            hasData: true,
            missingFacts: candidates.Count > 0 ? 0 : GetMissingFacts(disease, diseaseCase));
    }

    /// <summary>
    /// Проверяет выбранную партию. Неудачный синтез отбраковывает её,
    /// поэтому перебором вслепую тоже можно дойти до ответа, но дорогой ценой.
    /// </summary>
    /// <returns>True, если партия подходит штамму и вакцина получилась.</returns>
    public bool TrySynthesize(EntityUid disease, int candidateIndex)
    {
        if (!_caseQuery.TryComp(disease, out var diseaseCase) ||
            candidateIndex < 0 ||
            candidateIndex >= diseaseCase.Candidates.Count)
        {
            return false;
        }

        if (candidateIndex != diseaseCase.SolutionIndex)
        {
            diseaseCase.Candidates[candidateIndex].RuledOut = true;
            MarkChanged();
            return false;
        }

        // Рецепт найден: дальше машина штампует дозы по нему, уже без перебора.
        diseaseCase.Solved = true;
        MarkChanged();
        return true;
    }

    /// <summary>
    /// Есть ли в деле партия с таким индексом. Проверяется до расхода образца и сырья.
    /// </summary>
    public bool HasCandidate(EntityUid disease, int candidateIndex)
    {
        return _caseQuery.TryComp(disease, out var diseaseCase) &&
               candidateIndex >= 0 &&
               candidateIndex < diseaseCase.Candidates.Count;
    }

    /// <summary>
    /// Найден ли рецепт вакцины против штамма.
    /// </summary>
    public bool IsSolved(EntityUid disease)
    {
        return _caseQuery.TryComp(disease, out var diseaseCase) && diseaseCase.Solved;
    }

    #endregion

    #region Сборка партий

    /// <summary>
    /// Сообщает машинам, что дело изменилось.
    /// </summary>
    /// <remarks>
    /// Поднимается только из методов, которые меняют знания или прогресс. Из сборки состояния
    /// событие не поднимается: та сама вызывается из обработчиков и ушла бы в рекурсию.
    /// </remarks>
    private void MarkChanged()
    {
        var ev = new DiseaseResearchChangedEvent();
        RaiseLocalEvent(ref ev);
    }

    /// <summary>
    /// Открывает дело по штамму или возвращает уже открытое.
    /// </summary>
    /// <returns>False, если штамма в раунде нет.</returns>
    private bool TryGetCase(EntityUid disease, out DiseaseResearchCaseComponent diseaseCase)
    {
        if (_caseQuery.TryComp(disease, out diseaseCase!))
            return true;

        if (!_registry.StrainExists(disease))
            return false;

        diseaseCase = EnsureComp<DiseaseResearchCaseComponent>(disease);
        return true;
    }

    /// <summary>
    /// Считает, скольких подтверждённых фактов не хватает, чтобы собрать партии.
    /// </summary>
    /// <remarks>
    /// Нужно, чтобы панель говорила «не хватает двух наблюдений», а не бесполезное «нет данных»:
    /// у тяжёлого штамма одного обследованного пациента на загадку не хватает.
    /// </remarks>
    private int GetMissingFacts(EntityUid disease, DiseaseResearchCaseComponent diseaseCase)
    {
        if (!_registry.TryGetData(disease, out var proto) || _strength.GetDifficulty(disease) is not { } difficulty)
            return 1;

        var locked = Math.Clamp(difficulty.LockedClaims, 0, Math.Max(0, difficulty.Claims - 1));
        var visible = Math.Max(1, difficulty.Claims - locked);

        CollectPools(proto, diseaseCase, out var verifiable, out var refutable);

        // Без опровергаемых свойств негодные партии не из чего собрать: нужен класс возбудителя.
        var missing = visible + locked - verifiable.Count;
        if (refutable.Count == 0)
            missing = Math.Max(missing, 1);

        return Math.Max(missing, 0);
    }

    /// <summary>
    /// Строит пробные партии, когда данных анализов хватает на их проверку.
    /// До этого момента интерфейс партий не показывает: угадывать вслепую нечего.
    /// </summary>
    private void EnsureCandidates(EntityUid disease, DiseaseResearchCaseComponent diseaseCase)
    {
        if (diseaseCase.Candidates.Count > 0 || !_registry.TryGetData(disease, out var proto))
            return;

        if (_strength.GetDifficulty(disease) is not { } difficulty)
        {
            Log.Error($"No disease difficulty prototypes to size the vaccine puzzle for {ToPrettyString(disease)}");
            return;
        }

        // Хотя бы одно свойство должно быть видно сразу, иначе партии неразличимы.
        var locked = Math.Clamp(difficulty.LockedClaims, 0, Math.Max(0, difficulty.Claims - 1));
        var visible = Math.Max(1, difficulty.Claims - locked);

        CollectPools(proto, diseaseCase, out var verifiable, out var refutable);

        // Правильной партии нужны подтверждаемые свойства на все слоты, включая закрытые:
        // иначе её закрытый слот оказался бы нечем заполнить, и единственная нерасшифрованная
        // строка в панели сама указывала бы на ответ.
        if (verifiable.Count < visible + locked || refutable.Count == 0)
            return;

        BuildCandidates(diseaseCase, difficulty, visible, locked);
    }

    /// <summary>
    /// Собирает партии: одна верна во всех свойствах, остальные содержат ошибку.
    /// Ошибки раскладываются по ярусам расшифровки, поэтому партии отсеиваются волнами,
    /// а почти-правильная доживает до последнего замка.
    /// </summary>
    private void BuildCandidates(
        DiseaseResearchCaseComponent diseaseCase,
        DiseaseDifficultyPrototype difficulty,
        int visible,
        int locked)
    {
        var usedCodes = new HashSet<string>();
        var tiers = locked + 1;

        var solution = new DiseaseCandidate(GenerateCode(usedCodes), BuildSlots(difficulty, visible, locked, -1));
        var candidates = new List<DiseaseCandidate>(difficulty.Candidates) { solution };

        for (var i = 1; i < difficulty.Candidates; i++)
        {
            // Ярус 0 — ошибка видна сразу, остальные открываются по мере исследования.
            var tier = (i - 1) % tiers;
            candidates.Add(new DiseaseCandidate(GenerateCode(usedCodes), BuildSlots(difficulty, visible, locked, tier)));
        }

        _random.Shuffle(candidates);
        diseaseCase.Candidates.AddRange(candidates);
        diseaseCase.SolutionIndex = candidates.IndexOf(solution);
    }

    /// <summary>
    /// Раскладывает слоты партии: сначала видимые, потом закрытые по одному замку на ярус.
    /// Форма одинакова у всех партий, поэтому правильную не выдаёт ни число свойств, ни позиции замков.
    /// </summary>
    /// <param name="wrongTier">Ярус, в который попадает ошибка, или -1 для правильной партии.</param>
    private List<DiseaseClaimSlot> BuildSlots(
        DiseaseDifficultyPrototype difficulty,
        int visible,
        int locked,
        int wrongTier)
    {
        var slots = new List<DiseaseClaimSlot>(visible + locked);

        for (var i = 0; i < visible; i++)
        {
            slots.Add(new DiseaseClaimSlot(0));
        }

        for (var i = 0; i < locked; i++)
        {
            slots.Add(new DiseaseClaimSlot((i + 1) * difficulty.ResearchPerUnlock));
        }

        if (wrongTier < 0)
            return slots;

        if (wrongTier == 0)
        {
            // Ошибка среди видимых свойств: такую партию отбраковывают с первого же отчёта.
            var first = _random.Next(visible);
            slots[first].Wrong = true;

            // Вторая ошибка только у таких партий: у «поздних» ровно одна, и она откроется последней.
            if (visible > 1)
                slots[(first + 1 + _random.Next(visible - 1)) % visible].Wrong = true;

            return slots;
        }

        slots[visible + wrongTier - 1].Wrong = true;
        return slots;
    }

    /// <summary>
    /// Заполняет слоты, у которых подошёл прогресс исследования.
    /// </summary>
    /// <remarks>
    /// Свойство подбирается именно в момент расшифровки, из фактов, известных на тот момент.
    /// Заготовленное заранее свойство могло бы оказаться непроверяемым: игроку нечем было бы
    /// ни подтвердить его, ни опровергнуть.
    /// </remarks>
    private void FillSlots(EntityUid disease, DiseaseResearchCaseComponent diseaseCase)
    {
        if (!NeedsFilling(diseaseCase) || !_registry.TryGetData(disease, out var proto))
            return;

        CollectPools(proto, diseaseCase, out var verifiable, out var refutable);

        foreach (var candidate in diseaseCase.Candidates)
        {
            // Перемешиваем на каждую партию: иначе все они набрали бы свойства в одном порядке.
            _random.Shuffle(verifiable);
            _random.Shuffle(refutable);

            foreach (var slot in candidate.Slots)
            {
                if (slot.Claim != null || slot.RevealAt > diseaseCase.Research)
                    continue;

                // Если подходящего свойства пока нет, слот остаётся закрытым:
                // иначе ложная партия стала бы копией правильной.
                if (TryPickUnused(slot.Wrong ? refutable : verifiable, candidate, out var claim))
                    slot.Claim = claim;
            }
        }
    }

    private static bool NeedsFilling(DiseaseResearchCaseComponent diseaseCase)
    {
        foreach (var candidate in diseaseCase.Candidates)
        {
            foreach (var slot in candidate.Slots)
            {
                if (slot.Claim == null && slot.RevealAt <= diseaseCase.Research)
                    return true;
            }
        }

        return false;
    }

    private static bool TryPickUnused(
        List<DiseaseClaim> pool,
        DiseaseCandidate candidate,
        out DiseaseClaim claim)
    {
        claim = default;

        foreach (var candidateClaim in pool)
        {
            var used = false;
            foreach (var slot in candidate.Slots)
            {
                if (slot.Claim != candidateClaim)
                    continue;

                used = true;
                break;
            }

            if (used)
                continue;

            claim = candidateClaim;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Делит свойства на подтверждаемые и опровергаемые по текущим данным дела.
    /// </summary>
    /// <remarks>
    /// Свойства не хранятся прототипами, а собираются здесь: подтвердить можно только
    /// выявленный класс возбудителя и «гасит X» для замеченного симптома, а опровергнуть —
    /// чужой класс и «нестабилен при X», когда X у пациентов видели. Свойства про симптомы,
    /// которых у штамма нет, не берутся вовсе: отсутствие симптома отчётом не доказать.
    /// </remarks>
    private static void CollectPools(
        DiseaseDefinitionComponent proto,
        DiseaseResearchCaseComponent diseaseCase,
        out List<DiseaseClaim> verifiable,
        out List<DiseaseClaim> refutable)
    {
        verifiable = new List<DiseaseClaim>();
        refutable = new List<DiseaseClaim>();

        if (diseaseCase.ClassificationKnown)
        {
            verifiable.Add(new DiseaseClaim(DiseaseClaimKind.Class, proto.Classification, default));

            foreach (var classification in Enum.GetValues<DiseaseClassification>())
            {
                if (classification != proto.Classification)
                    refutable.Add(new DiseaseClaim(DiseaseClaimKind.Class, classification, default));
            }
        }

        foreach (var symptom in diseaseCase.ObservedSymptoms)
        {
            verifiable.Add(new DiseaseClaim(DiseaseClaimKind.Suppresses, default, symptom));
            refutable.Add(new DiseaseClaim(DiseaseClaimKind.UnstableWith, default, symptom));
        }
    }

    /// <summary>
    /// Возвращает строку свойства для интерфейса.
    /// </summary>
    private bool TryGetClaimText(DiseaseClaim claim, out string text)
    {
        if (claim.Kind == DiseaseClaimKind.Class)
        {
            text = DiseaseLocalization.GetClassClaimLocId(claim.Classification).Id;
            return true;
        }

        if (_proto.TryIndex(claim.Symptom, out var symptom))
        {
            var locId = claim.Kind == DiseaseClaimKind.Suppresses ? symptom.SuppressClaim : symptom.UnstableClaim;
            text = locId.Id;
            return true;
        }

        text = string.Empty;
        return false;
    }

    private string GenerateCode(HashSet<string> usedCodes)
    {
        string code;
        do
        {
            code = $"V-{_random.Next(100, 1000)}";
        }
        while (!usedCodes.Add(code));

        return code;
    }

    #endregion
}
