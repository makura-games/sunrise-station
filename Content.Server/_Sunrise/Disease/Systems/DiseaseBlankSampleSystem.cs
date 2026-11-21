using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Labels.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Однократно выбирает параметры заготовки из диапазонов её прототипа.
/// </summary>
public sealed partial class DiseaseBlankSampleSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private LabelSystem _label = default!;

    [SubscribeLocalEvent]
    private void OnSampleMapInit(Entity<DiseaseStrainSampleComponent> ent, ref MapInitEvent args)
    {
        // В сохранённой заготовке уже нет генератора: восстанавливаем признак по её параметрам.
        ent.Comp.IsBlank = IsPreparedBlank(ent);
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<DiseaseBlankSampleComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<DiseaseStrainSampleComponent>(ent, out var sample))
            return;

        if (PickProfile(ent.Comp) is not { } profile)
            return;

        var candidates = new List<DiseaseSymptomEntry>();
        var seen = new HashSet<ProtoId<DiseaseSymptomPrototype>>();
        foreach (var entry in ent.Comp.Symptoms)
        {
            // Полезные симптомы идут отдельным броском, в общий пул они не попадают.
            if (IsValidEntry(entry) && !IsBeneficial(entry.Symptom) && seen.Add(entry.Symptom))
                candidates.Add(entry);
        }

        var min = Math.Clamp(profile.MinSymptoms, 0, candidates.Count);
        var max = Math.Clamp(profile.MaxSymptoms, min, candidates.Count);
        var count = _random.Next(min, max + 1);
        var pool = new List<DiseaseSymptomEntry>(count);
        while (pool.Count < count)
        {
            var index = PickWeightedIndex(candidates);
            var picked = candidates[index];
            pool.Add(new DiseaseSymptomEntry { Symptom = picked.Symptom, Weight = picked.Weight });
            candidates.RemoveAt(index);
        }

        var minStage = Math.Clamp(profile.MinMaxStage, 0, count);
        var maxStage = Math.Clamp(profile.MaxMaxStage, minStage, count);
        sample.Disease = null;
        sample.HasStrain = false;
        sample.StrainCode = $"SYN-{ent.Owner.Id}";
        sample.SymptomPool = pool;
        sample.Stage = 0;

        // На каждую стадию нужны минимум две разные колонии: цель и конкурент.
        // Длина вектора ограничена ещё и архетипами болезней, под которые его потом выпускают.
        var stageOptions = Math.Max(ent.Comp.MinStageOptions, 1);
        sample.MaxStage = Math.Clamp(_random.Next(minStage, maxStage + 1), 2,
            Math.Clamp(pool.Count / stageOptions, 2, Math.Max(ent.Comp.MaxStages, 2)));
        sample.StagePools = BuildStagePools(ent.Comp, pool, sample.MaxStage);
        AddFillers(ent.Comp, sample.StagePools, pool);
        TryAddBeneficial(ent.Comp, profile, sample.StagePools, pool);
        sample.Genome = new();
        sample.Resistance = Math.Clamp(_random.NextFloat(profile.MinResistance, profile.MaxResistance), 0f, 1f);
        sample.Developed = new();
        sample.InfoRevealed = false;
        sample.IsBlank = IsPreparedBlank((ent.Owner, sample));
        sample.IsDesigned = false;
        Dirty(ent, sample);
        _label.Label(ent, sample.StrainCode);

        // Генератор отработал и дальше только мешает: его наличие и означает «уровень ещё не выбран»,
        // поэтому раскатанная заготовка уходит в сохранённую карту уже без него.
        RemCompDeferred<DiseaseBlankSampleComponent>(ent);
    }

    /// <summary>Проверяет готовую заготовку, в том числе восстановленную из сохранённой карты.</summary>
    public bool IsPreparedBlank(Entity<DiseaseStrainSampleComponent> sample)
    {
        var comp = sample.Comp;
        if (TerminatingOrDeleted(sample) || comp.Disease != null || comp.MaxStage <= 0 ||
            !float.IsFinite(comp.Resistance) || comp.SymptomPool.Count == 0 ||
            comp.StagePools.Count < comp.MaxStage || comp.Stage < 0 || comp.Stage > comp.MaxStage)
        {
            return false;
        }

        foreach (var pool in comp.StagePools)
        {
            foreach (var entry in pool.Symptoms)
            {
                if (!IsValidEntry(entry))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Раскладывает пул заготовки по стадиям развития: ранние стадии набираются из лёгких симптомов,
    /// поздние — из тяжёлых, поэтому партия под микроскопом усложняется вместе с геномом.
    /// </summary>
    /// <remarks>
    /// Сколько вариантов попадёт на стадию, задают <see cref="DiseaseBlankSampleComponent.MinStageOptions"/>
    /// и <see cref="DiseaseBlankSampleComponent.MaxStageOptions"/>: один вариант станет целью партии,
    /// другой — конкурентом, которого на этой стадии развить уже нельзя, остальные идут в запас.
    /// </remarks>
    private List<DiseaseStagePool> BuildStagePools(DiseaseBlankSampleComponent blank, List<DiseaseSymptomEntry> pool, int stages)
    {
        var ordered = new List<DiseaseSymptomEntry>(pool);
        ordered.Sort((first, second) => GetSeverity(first).CompareTo(GetSeverity(second)));

        var minOptions = Math.Max(blank.MinStageOptions, 1);
        var maxOptions = Math.Max(blank.MaxStageOptions, minOptions);

        var pools = new List<DiseaseStagePool>(stages);
        var cursor = 0;
        for (var stage = 0; stage < stages; stage++)
        {
            var left = ordered.Count - cursor;
            var size = Math.Clamp(left / (stages - stage), minOptions, maxOptions);
            size = Math.Clamp(size, 0, left);
            pools.Add(new DiseaseStagePool { Symptoms = ordered.GetRange(cursor, size) });
            cursor += size;
        }

        return pools;
    }

    /// <summary>
    /// Изредка добавляет полезный симптом на последнюю стадию заготовки.
    /// </summary>
    /// <remarks>
    /// Последняя стадия разыгрывается самой трудной партией, поэтому бафф достаётся только тому,
    /// кто довёл геном до конца и не отдал полезный симптом колонии-конкуренту. Стадия при этом
    /// может получить на один вариант больше обычного потолка.
    /// </remarks>
    private void TryAddBeneficial(
        DiseaseBlankSampleComponent blank,
        DiseaseBlankSampleProfile profile,
        List<DiseaseStagePool> stagePools,
        List<DiseaseSymptomEntry> pool)
    {
        if (stagePools.Count == 0 || !_random.Prob(Math.Clamp(profile.BeneficialChance, 0f, 1f)))
            return;

        var candidates = new List<DiseaseSymptomEntry>();
        foreach (var entry in blank.BeneficialSymptoms)
        {
            if (IsValidEntry(entry) && IsBeneficial(entry.Symptom) && !candidates.Exists(c => c.Symptom == entry.Symptom))
                candidates.Add(entry);
        }

        if (candidates.Count == 0)
            return;

        var picked = candidates[PickWeightedIndex(candidates)];
        var added = new DiseaseSymptomEntry { Symptom = picked.Symptom, Weight = picked.Weight };
        stagePools[^1].Symptoms.Add(added);
        pool.Add(added);
    }

    /// <summary>
    /// С шансом заготовки добавляет каждой стадии пустой симптом сверх её вариантов.
    /// </summary>
    /// <remarks>
    /// Пробел даёт вирусологу безвредный выбор на стадии, но разыгрывается той же партией,
    /// что и любой другой симптом, и может достаться колонии-конкуренту.
    /// </remarks>
    private void AddFillers(DiseaseBlankSampleComponent blank, List<DiseaseStagePool> stagePools, List<DiseaseSymptomEntry> pool)
    {
        var fillers = new List<DiseaseSymptomEntry>();
        foreach (var entry in blank.FillerSymptoms)
        {
            if (IsValidEntry(entry) && !fillers.Exists(f => f.Symptom == entry.Symptom))
                fillers.Add(entry);
        }

        if (fillers.Count == 0)
            return;

        var chance = Math.Clamp(blank.FillerChance, 0f, 1f);
        foreach (var stage in stagePools)
        {
            if (!_random.Prob(chance))
                continue;

            var picked = fillers[PickWeightedIndex(fillers)];
            var added = new DiseaseSymptomEntry { Symptom = picked.Symptom, Weight = picked.Weight };
            stage.Symptoms.Add(added);

            // Общий пул заготовки хранит каждый симптом один раз, даже если пробел выпал на нескольких стадиях.
            if (!pool.Exists(entry => entry.Symptom == picked.Symptom))
                pool.Add(added);
        }
    }

    private int PickWeightedIndex(List<DiseaseSymptomEntry> candidates)
    {
        var total = 0f;
        foreach (var candidate in candidates)
            total += candidate.Weight;

        var roll = _random.NextFloat() * total;
        for (var i = 0; i < candidates.Count; i++)
        {
            roll -= candidates[i].Weight;
            if (roll <= 0f)
                return i;
        }

        return candidates.Count - 1;
    }

    private bool IsValidEntry(DiseaseSymptomEntry entry)
    {
        return entry.Weight > 0f && float.IsFinite(entry.Weight) && _prototype.HasIndex(entry.Symptom);
    }

    private bool IsBeneficial(ProtoId<DiseaseSymptomPrototype> symptom)
    {
        return _prototype.TryIndex(symptom, out var proto) && proto.Beneficial;
    }

    private float GetSeverity(DiseaseSymptomEntry entry)
    {
        return _prototype.TryIndex(entry.Symptom, out var symptom) ? symptom.Severity : 1f;
    }

    private DiseaseBlankSampleProfile? PickProfile(DiseaseBlankSampleComponent comp)
    {
        // Заданный уровень берётся напрямую, веса профилей при этом не учитываются.
        if (comp.Quality is { } quality)
            return comp.Profiles.GetValueOrDefault(quality);

        var total = 0f;
        foreach (var profile in comp.Profiles.Values)
        {
            if (float.IsFinite(profile.Weight) && profile.Weight > 0f)
                total += profile.Weight;
        }

        if (!float.IsFinite(total) || total <= 0f)
            return null;

        var roll = _random.NextFloat() * total;
        DiseaseBlankSampleProfile? fallback = null;
        foreach (var profile in comp.Profiles.Values)
        {
            if (!float.IsFinite(profile.Weight) || profile.Weight <= 0f)
                continue;

            fallback = profile;
            roll -= profile.Weight;
            if (roll <= 0f)
                return profile;
        }

        return fallback;
    }
}
