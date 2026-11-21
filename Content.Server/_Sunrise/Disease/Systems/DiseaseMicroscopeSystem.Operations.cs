using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Microscope;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Disease.Systems;

public sealed partial class DiseaseMicroscopeSystem
{
    // Ход игрока, ответ конкурента и закрепление результата стадии в геноме.

    /// <summary>Останавливает партию и закрепляет её результат в геноме заготовки.</summary>
    public bool TryFinish(Entity<DiseaseMicroscopeComponent> ent, int revision)
    {
        RefreshAndPublish(ent);
        if (!CanFinish(ent, revision, out var feedback) || !TryGetSample(ent, out var sample))
        {
            CompleteCommand(ent, feedback);
            return false;
        }

        var comp = ent.Comp;
        var target = DiseaseCultureBoard.CountColony(comp.Cells, DiseaseCultureBoard.Target);
        var opponent = DiseaseCultureBoard.CountColony(comp.Cells, DiseaseCultureBoard.Opponent);
        var reached = comp.Profile.IsReached(target, opponent);
        var route = comp.TargetRoute;
        var symptom = comp.TargetSymptom;
        var bonus = comp.BonusMatch;

        ResetMatch(comp);
        if (!reached)
        {
            // Провал стоит только расходников: стадия остаётся как была, заготовка цела.
            CompleteCommand(ent, "failed");
            _audio.PlayPvs(ent.Comp.FailureSound, ent);
            return false;
        }

        if (route != DiseaseTransmissionRoute.None)
            sample.Comp.Routes |= route;
        else if (bonus)
            DoReinforce(sample, symptom!.Value);
        else
            DoDevelop(sample, symptom!.Value);

        // Выигранная партия закаляет культуру. Считается до обновления панели: следующая партия
        // уже идёт по новой устойчивости, а сосуд получит окрепший штамм.
        sample.Comp.Resistance = Math.Clamp(sample.Comp.Resistance + sample.Comp.ResistanceGain, 0f, 1f);

        CompleteCommand(ent, route != DiseaseTransmissionRoute.None ? "transmission-developed" : bonus ? "reinforced" : "developed");
        PlaySuccess(ent);
        return true;
    }

    /// <summary>Закрывает стадию окончательно и переводит заготовку к следующей.</summary>
    public bool TryAdvance(Entity<DiseaseMicroscopeComponent> ent, int revision)
    {
        RefreshAndPublish(ent);
        if (!CanRun(ent, revision, out var sample, out var feedback))
        {
            CompleteCommand(ent, feedback);
            return false;
        }

        if (!ent.Comp.StageOpen || IsMatchValid(ent.Comp))
        {
            CompleteCommand(ent, "no-match");
            return false;
        }

        sample.Comp.StageOpen = false;
        CompleteCommand(ent, ent.Comp.Stage >= ent.Comp.MaxStage ? "genome-complete" : "advanced");
        PlaySuccess(ent);
        return true;
    }

    /// <summary>Проверяет, есть ли что завершать, не трогая поле.</summary>
    public bool CanFinish(Entity<DiseaseMicroscopeComponent> ent, int revision, out string feedback)
    {
        if (!CanRun(ent, revision, out _, out feedback))
            return false;

        feedback = "no-match";
        return IsMatchValid(ent.Comp);
    }

    /// <summary>
    /// Закрывает стадию заготовки выращенным симптомом и оставляет её открытой для усиления.
    /// </summary>
    /// <remarks>
    /// Выигранная партия даёт чистую стадию: конкурент в пул не попадает,
    /// поэтому носителю достаётся ровно выбранный симптом.
    /// </remarks>
    private static void DoDevelop(Entity<DiseaseStrainSampleComponent> sample, ProtoId<DiseaseSymptomPrototype> symptom)
    {
        var stage = new DiseaseStagePool { Developed = 1 };
        stage.Symptoms.Add(new DiseaseSymptomEntry { Symptom = symptom, Weight = DevelopedWeight });

        sample.Comp.Genome = new List<DiseaseStagePool>(sample.Comp.Genome) { stage };
        sample.Comp.Developed = new List<ProtoId<DiseaseSymptomPrototype>>(sample.Comp.Developed) { symptom };
        sample.Comp.Stage = Math.Min(sample.Comp.Stage + 1, sample.Comp.MaxStage);
        sample.Comp.StageOpen = true;
    }

    /// <summary>
    /// Дописывает ещё один симптом в последнюю стадию: это награда за отдельную, более трудную партию.
    /// </summary>
    private static void DoReinforce(Entity<DiseaseStrainSampleComponent> sample, ProtoId<DiseaseSymptomPrototype> symptom)
    {
        if (sample.Comp.Genome.Count == 0)
            return;

        var genome = DiseaseMicroscopeSystem.CopyGenome(sample.Comp.Genome);
        var stage = genome[^1];

        // Стадия выведенного штамма проявляет все свои симптомы, поэтому усиление добавляет ещё один.
        stage.Symptoms.Add(new DiseaseSymptomEntry { Symptom = symptom, Weight = DevelopedWeight });
        stage.Developed++;

        sample.Comp.Genome = genome;
        sample.Comp.Developed = new List<ProtoId<DiseaseSymptomPrototype>>(sample.Comp.Developed) { symptom };
    }

    /// <summary>Выпускает собранный геном отдельным штаммом и расходует заготовку.</summary>
    public bool TryProduce(Entity<DiseaseMicroscopeComponent> ent, int revision, EntityUid? user = null)
    {
        RefreshAndPublish(ent);
        if (!CanProduce(ent, revision, out var feedback) || !TryGetSample(ent, out var sample))
        {
            CompleteCommand(ent, feedback);
            return false;
        }

        if (!DoProduce(ent, sample, user))
        {
            CompleteCommand(ent, "invalid-sample");
            return false;
        }

        CompleteCommand(ent, "produced");
        PlaySuccess(ent);
        return true;
    }

    /// <summary>Проверяет геном и свободный архетип, ничего не создавая.</summary>
    public bool CanProduce(Entity<DiseaseMicroscopeComponent> ent, int revision, out string feedback)
    {
        if (!CanRun(ent, revision, out _, out feedback))
            return false;

        feedback = "empty-genome";
        if (ent.Comp.Genome.Count == 0)
            return false;

        feedback = "invalid-sample";
        if (!_prototype.HasIndex(ent.Comp.DesignedArchetype))
            return false;

        return _prototype.TryIndex(ent.Comp.OutputPrototype, out var output) &&
               output.HasComp<DiseaseStrainSampleComponent>(EntityManager.ComponentFactory);
    }

    private bool DoProduce(Entity<DiseaseMicroscopeComponent> ent, Entity<DiseaseStrainSampleComponent> sample, EntityUid? user)
    {
        var genome = CopyGenome(ent.Comp.Genome);
        var pool = new List<DiseaseSymptomEntry>();
        var developed = new List<ProtoId<DiseaseSymptomPrototype>>(genome.Count);
        foreach (var stage in genome)
        {
            // Ведущий симптом стадии — тот, что вытянули в партии; он и попадает в карточку штамма.
            var leading = stage.Symptoms[0];
            foreach (var entry in stage.Symptoms)
            {
                if (entry.Weight > leading.Weight)
                    leading = entry;

                pool.Add(new DiseaseSymptomEntry { Symptom = entry.Symptom, Weight = entry.Weight });
            }

            developed.Add(leading.Symptom);
        }

        // Болезнь заводится до появления пробирки: иначе отказ лаборатории оставил бы пустой образец.
        if (_registry.CreateDesigned(ent.Comp.DesignedArchetype, genome, sample.Comp.Routes,
                sample.Comp.Resistance) is not { } disease)
            return false;

        // Лаборатория сама собрала штамм, поэтому его симптомы и класс сразу ложатся в дело:
        // заново выяснять анализами то, что вирусолог выращивал вручную, незачем.
        var observed = new List<ProtoId<DiseaseSymptomPrototype>>();
        foreach (var stage in genome)
        {
            foreach (var entry in stage.Symptoms)
            {
                if (!observed.Contains(entry.Symptom))
                    observed.Add(entry.Symptom);
            }
        }

        _research.RevealFacts(disease, observed, true);

        var uid = SpawnNextToOrDrop(ent.Comp.OutputPrototype, ent);
        var output = Comp<DiseaseStrainSampleComponent>(uid);
        output.Disease = disease;
        output.HasStrain = true;
        output.IsBlank = false;
        output.IsDesigned = true;
        output.Routes = sample.Comp.Routes;
        output.StrainCode = _analysis.GetStrainCode(disease);
        output.SymptomPool = pool;
        output.StagePools = new();
        output.Genome = genome;
        output.Developed = developed;
        output.Stage = developed.Count;
        output.MaxStage = developed.Count;
        output.Resistance = sample.Comp.Resistance;
        output.InfoRevealed = true;
        Dirty(uid, output);
        _label.Label(uid, output.StrainCode);

        _adminLog.Add(LogType.Disease, LogImpact.High,
            $"{ToPrettyString(user):user} released designed strain {ToPrettyString(disease)} ({output.StrainCode}) from {string.Join(", ", developed)} in {ToPrettyString(ent.Owner):microscope}");

        // Заготовка превращается в штамм целиком, иначе один геном можно было бы разлить несколько раз.
        _itemSlots.TryEject(ent.Owner, ent.Comp.SampleSlotId, null, out _);
        QueueDel(sample.Owner);
        ResetMatch(ent.Comp);
        return true;
    }

    private void PlaySuccess(Entity<DiseaseMicroscopeComponent> ent, DiseaseMicroscopeTool? tool = null)
    {
        // Клиент не предсказывает лабораторную операцию, поэтому звук отправляется и её автору.
        var sound = tool switch
        {
            DiseaseMicroscopeTool.Nutrient => ent.Comp.NutrientSound,
            DiseaseMicroscopeTool.Inhibitor => ent.Comp.InhibitorSound,
            _ => ent.Comp.SuccessSound,
        };
        _audio.PlayPvs(sound, ent);
    }
}
