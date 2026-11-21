using Content.Server._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Events;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Database;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Disease.Systems;

public sealed partial class DiseaseSystem
{
    /*
     * Часть системы про симптомы: взвешенный выбор при входе в стадию, броски симптомов и их снятие.
     * Симптомы накапливаются от стадии к стадии, поэтому у двух носителей одной болезни картина может отличаться.
     */

    /// <summary>
    /// Проверяет, проявился ли симптом у данного экземпляра болезни.
    /// </summary>
    public static bool HasSymptom(DiseaseInstance instance, ProtoId<DiseaseSymptomPrototype> symptom)
    {
        foreach (var active in instance.Symptoms)
        {
            if (active.Symptom == symptom)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Выбирает новые симптомы из пула стадии с учётом весов, пропуская уже проявившиеся.
    /// </summary>
    private void PickSymptoms(EntityUid uid, DiseaseInstance instance, DiseaseStage stage)
    {
        var pool = _registry.GetStagePool(instance.Disease, instance.Stage);
        if (pool.Count == 0 || stage.MaxSymptoms <= 0)
            return;

        var candidates = new List<DiseaseSymptomEntry>(pool.Count);
        var totalWeight = 0f;
        foreach (var entry in pool)
        {
            if (entry.Weight <= 0f || HasSymptom(instance, entry.Symptom))
                continue;

            candidates.Add(entry);
            totalWeight += entry.Weight;
        }

        var minCount = Math.Max(stage.MinSymptoms, 0);
        var count = _random.Next(minCount, Math.Max(minCount, stage.MaxSymptoms) + 1);
        var picked = new List<ProtoId<DiseaseSymptomPrototype>>(count);

        // Штамм с воздушным путём обязан кашлять или чихать: иначе капли не разлетятся вовсе.
        if (_registry.TryGetData(instance.Disease, out var data) && data.DropletStage == instance.Stage &&
            data.DropletSymptom is { } droplet)
        {
            var index = candidates.FindIndex(entry => entry.Symptom == droplet);
            if (index >= 0)
            {
                totalWeight -= candidates[index].Weight;
                picked.Add(droplet);
                candidates.RemoveAt(index);
                count = Math.Max(count, 1);
            }
        }

        while (picked.Count < count && candidates.Count > 0)
        {
            var index = PickWeightedIndex(candidates, totalWeight);
            totalWeight -= candidates[index].Weight;
            picked.Add(candidates[index].Symptom);
            candidates.RemoveAt(index);
        }

        foreach (var symptom in picked)
        {
            AddSymptom(uid, instance, symptom);
        }
    }

    private int PickWeightedIndex(List<DiseaseSymptomEntry> candidates, float totalWeight)
    {
        var roll = _random.NextFloat() * totalWeight;
        for (var i = 0; i < candidates.Count; i++)
        {
            roll -= candidates[i].Weight;
            if (roll < 0f)
                return i;
        }

        // Погрешность float может оставить остаток после последнего кандидата.
        return candidates.Count - 1;
    }

    private void AddSymptom(EntityUid uid, DiseaseInstance instance, ProtoId<DiseaseSymptomPrototype> symptomId)
    {
        if (!_proto.TryIndex(symptomId, out var symptom))
        {
            Log.Error($"Disease {ToPrettyString(instance.Disease)} tried to add unknown symptom {symptomId} to {ToPrettyString(uid)}");
            return;
        }

        instance.Symptoms.Add(new DiseaseActiveSymptom(symptomId, instance.Stage));

        _adminLog.Add(LogType.Disease, LogImpact.Low,
            $"{ToPrettyString(uid):target} developed symptom {symptomId} of {ToPrettyString(instance.Disease)}");

        var ev = new DiseaseSymptomOnsetEvent(instance.Disease, symptomId);
        RaiseLocalEvent(uid, ref ev);

        if (symptom.OnsetEffects is { } onsetEffects)
            _effects.ApplyEffects(uid, onsetEffects);
    }

    /// <summary>
    /// Снимает симптомы, выбранные на стадии <paramref name="fromStage"/> и более поздних.
    /// </summary>
    private void ResolveSymptoms(EntityUid uid, DiseaseInstance instance, int fromStage)
    {
        for (var i = instance.Symptoms.Count - 1; i >= 0; i--)
        {
            if (i >= instance.Symptoms.Count)
                continue;

            var active = instance.Symptoms[i];
            if (active.Stage < fromStage)
                continue;

            instance.Symptoms.RemoveAt(i);

            var ev = new DiseaseSymptomResolvedEvent(instance.Disease, active.Symptom);
            RaiseLocalEvent(uid, ref ev);
        }
    }

    private void RollSymptoms(
        Entity<DiseaseCarrierComponent> ent,
        DiseaseInstance instance,
        DiseaseDefinitionComponent proto,
        bool suppressed)
    {
        var stage = proto.Stages[instance.Stage];

        for (var i = 0; i < instance.Symptoms.Count; i++)
        {
            if (!_proto.TryIndex(instance.Symptoms[i].Symptom, out var symptom))
                continue;

            if (suppressed && symptom.Suppressible)
                continue;

            if (!_random.Prob(Math.Clamp(symptom.Probability, 0f, 1f)))
                continue;

            _effects.ApplyEffects(ent, symptom.Effects);

            // Эффекты симптома могли вылечить эту болезнь.
            if (!ent.Comp.Diseases.Contains(instance))
                return;

            if (symptom.Spread is { } spread)
                SpreadDroplets(ent, instance, proto, stage, spread);
        }
    }
}
