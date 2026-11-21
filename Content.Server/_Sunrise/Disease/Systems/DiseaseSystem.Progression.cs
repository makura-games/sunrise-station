using Content.Server._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Events;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Bed.Sleep;
using Content.Shared.Database;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.Disease.Systems;

public sealed partial class DiseaseSystem
{
    /*
     * Часть системы про развитие болезни: периодическая обработка носителей и вероятностная смена стадий.
     * Активная болезнь продвигается вперёд, а при выздоровлении откатывается по стадиям назад.
     */

    /// <summary>
    /// Шаг цикла развития. За один шаг каждый таймер болезни растёт ровно на эту величину.
    /// </summary>
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Носители, отобранные на текущий шаг. Поле переиспользуется, чтобы не выделять список каждый тик.
    /// </summary>
    private readonly List<Entity<DiseaseCarrierComponent>> _updateBuffer = [];

    [Dependency] private EntityQuery<SleepingComponent> _sleepingQuery = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        // Сначала снимок носителей: симптомы заражают соседей, а новый компонент носителя
        // ломает перебор запроса прямо посреди обхода.
        _updateBuffer.Clear();
        var query = EntityQueryEnumerator<DiseaseCarrierComponent>();
        while (query.MoveNext(out var uid, out var carrier))
        {
            if (carrier.NextUpdate > curTime)
                continue;

            carrier.NextUpdate = curTime + UpdateInterval;
            _updateBuffer.Add((uid, carrier));
        }

        foreach (var ent in _updateBuffer)
        {
            // Пока шёл обход, носителя могли удалить или вылечить.
            if (TerminatingOrDeleted(ent.Owner) || ent.Comp.LifeStage > ComponentLifeStage.Running)
                continue;

            // У мёртвого носителя болезнь не прогрессирует и не даёт симптомов.
            if (_mobState.IsDead(ent.Owner))
                continue;

            ProcessCarrier(ent);
        }

        _updateBuffer.Clear();
    }

    /// <summary>
    /// Переводит болезнь носителя на другую стадию по одному шагу, как при естественном развитии:
    /// при движении вперёд выбираются симптомы каждой пройденной стадии, при движении назад снимаются симптомы покинутых.
    /// </summary>
    /// <returns>False, если носитель не болен этой болезнью или стадия вне диапазона.</returns>
    public bool TrySetStage(Entity<DiseaseCarrierComponent?> ent, EntityUid disease, int stage)
    {
        if (!TryGetDisease(ent, disease, out var instance) ||
            !_registry.TryGetData(disease, out var proto) ||
            stage < 0 ||
            stage >= proto.Stages.Count)
        {
            return false;
        }

        while (instance.Stage < stage)
        {
            AdvanceStage(ent.Owner, instance, proto);
        }

        while (instance.Stage > stage)
        {
            StepBack(ent.Owner, instance);
        }

        return true;
    }

    private void ProcessCarrier(Entity<DiseaseCarrierComponent> ent)
    {
        // Обратный обход: болезнь может быть вылечена прямо во время обработки.
        for (var i = ent.Comp.Diseases.Count - 1; i >= 0; i--)
        {
            // Эффекты симптомов могут вылечить, добавить болезнь или оживить носителя, поэтому список перепроверяется на каждом шаге.
            if (ent.Comp.LifeStage > ComponentLifeStage.Running)
                return;

            if (i >= ent.Comp.Diseases.Count)
                continue;

            var instance = ent.Comp.Diseases[i];
            if (!_registry.TryGetData(instance.Disease, out var proto) || proto.Stages.Count == 0)
            {
                Log.Error($"Removing invalid disease {ToPrettyString(instance.Disease)} from {ToPrettyString(ent)}");
                Cure(ent, i, DiseaseCureReason.Admin);
                continue;
            }

            ProcessDisease(ent, instance, proto);
        }
    }

    private void ProcessDisease(Entity<DiseaseCarrierComponent> ent, DiseaseInstance instance, DiseaseDefinitionComponent proto)
    {
        var suppressed = instance.Suppressed;
        if (suppressed)
            instance.SuppressionRemaining = Decay(instance.SuppressionRemaining);

        // Снятие симптомов глушит проявления, но не трогает ни развитие, ни заразность.
        var relieved = instance.Relieved;
        if (relieved)
            instance.ReliefRemaining = Decay(instance.ReliefRemaining);

        // Подавление держит болезнь на месте, но не мешает выздоровлению.
        var paused = suppressed && proto.SuppressionPausesProgression && !instance.Recovering;
        if (!paused)
            instance.StageElapsed += UpdateInterval;

        instance.ProcessAccumulator += UpdateInterval;
        if (instance.ProcessAccumulator < proto.ProcessInterval)
            return;

        instance.ProcessAccumulator -= proto.ProcessInterval;
        RollSymptoms(ent, instance, proto, suppressed || relieved);

        // Эффекты симптомов могли вылечить эту болезнь.
        if (!ent.Comp.Diseases.Contains(instance))
            return;

        // Сон помогает отбиться от лёгкой заразы и тогда, когда развитие остановлено подавлением.
        TrySleepCure(ent, instance, proto);

        if (paused)
            return;

        TryProgress(ent, instance, proto);
    }

    /// <summary>
    /// Копит прогресс выздоровления, пока носитель спит.
    /// </summary>
    /// <remarks>
    /// Организм справляется сам, если дать ему отлежаться, поэтому иммунитет выдаётся
    /// как при естественном выздоровлении. Болезни без <see cref="DiseaseDefinitionComponent.SleepCureTime"/>
    /// сном не лечатся: от тяжёлой заразы отлежаться нельзя.
    /// </remarks>
    private void TrySleepCure(EntityUid uid, DiseaseInstance instance, DiseaseDefinitionComponent proto)
    {
        if (instance.Recovering || proto.SleepCureTime <= TimeSpan.Zero || !_sleepingQuery.HasComp(uid))
            return;

        var share = (float)(proto.ProcessInterval / proto.SleepCureTime);
        AddProgress(uid, instance, proto, proto.CureThreshold * share, DiseaseCureReason.Natural);
    }

    private void TryProgress(Entity<DiseaseCarrierComponent> ent, DiseaseInstance instance, DiseaseDefinitionComponent proto)
    {
        if (instance.Recovering)
        {
            if (RollProgression(proto.Regression, instance.StageElapsed))
                Regress(ent, instance);

            return;
        }

        if (!RollProgression(proto.Stages[instance.Stage].Advance, instance.StageElapsed))
            return;

        // Болезнь прошла последнюю стадию и начинает отступать сама.
        if (instance.Stage + 1 >= proto.Stages.Count)
        {
            BeginRecovery(ent, instance, DiseaseCureReason.Natural);
            return;
        }

        AdvanceStage(ent, instance, proto);
    }

    private static TimeSpan Decay(TimeSpan remaining)
    {
        var left = remaining - UpdateInterval;
        return left < TimeSpan.Zero ? TimeSpan.Zero : left;
    }

    private bool RollProgression(DiseaseProgression progression, TimeSpan elapsed)
    {
        if (elapsed < progression.MinDuration)
            return false;

        if (progression.MaxDuration is { } max && elapsed >= max)
            return true;

        return progression.Chance > 0f && _random.Prob(MathF.Min(progression.Chance, 1f));
    }

    private void BeginRecovery(EntityUid uid, DiseaseInstance instance, DiseaseCureReason reason)
    {
        if (instance.Recovering)
            return;

        instance.Recovering = true;
        instance.RecoveryReason = reason;
        instance.StageElapsed = TimeSpan.Zero;

        _adminLog.Add(LogType.Disease, LogImpact.Low,
            $"{ToPrettyString(uid):target} started recovering from {ToPrettyString(instance.Disease)} ({reason})");

        var ev = new DiseaseRecoveryStartedEvent(instance.Disease, reason);
        RaiseLocalEvent(uid, ref ev);
    }

    /// <summary>
    /// Откатывает выздоравливающую болезнь на стадию назад или удаляет её после стадии 0.
    /// </summary>
    private void Regress(Entity<DiseaseCarrierComponent> ent, DiseaseInstance instance)
    {
        if (instance.Stage > 0)
        {
            StepBack(ent, instance);
            return;
        }

        var index = ent.Comp.Diseases.IndexOf(instance);
        if (index >= 0)
            Cure(ent, index, instance.RecoveryReason);
    }

    private void AdvanceStage(EntityUid uid, DiseaseInstance instance, DiseaseDefinitionComponent proto)
    {
        ChangeStage(uid, instance, instance.Stage + 1);
        EnterStage(uid, instance, proto);
    }

    private void StepBack(EntityUid uid, DiseaseInstance instance)
    {
        ResolveSymptoms(uid, instance, fromStage: instance.Stage);
        ChangeStage(uid, instance, instance.Stage - 1);
    }

    /// <summary>
    /// Применяет эффекты входа в текущую стадию и выбирает её симптомы.
    /// </summary>
    private void EnterStage(EntityUid uid, DiseaseInstance instance, DiseaseDefinitionComponent proto)
    {
        var stage = proto.Stages[instance.Stage];
        if (stage.EnterEffects is { } enterEffects)
            _effects.ApplyEffects(uid, enterEffects);

        PickSymptoms(uid, instance, stage);
    }

    private void ChangeStage(EntityUid uid, DiseaseInstance instance, int newStage)
    {
        var oldStage = instance.Stage;
        instance.Stage = newStage;
        instance.StageElapsed = TimeSpan.Zero;

        _adminLog.Add(LogType.Disease, LogImpact.Low,
            $"{ToPrettyString(uid):target} moved from stage {oldStage} to stage {newStage} of {ToPrettyString(instance.Disease)}");

        var ev = new DiseaseStageChangedEvent(instance.Disease, oldStage, newStage);
        RaiseLocalEvent(uid, ref ev);
    }
}
