using Content.Shared._Sunrise.Disease.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.StepTrigger.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Disease.Systems;

/// <summary>
/// Стерилизующая арка: проход через неё тратит порцию дезинфектанта и обдаёт прошедшего.
/// </summary>
/// <remarks>
/// Распыление предсказывается: звук, сообщение и вспышка арки появляются у прошедшего сразу.
/// Обработка вещей и проверка на заразу — на сервере в <see cref="OnPassed"/>, потому что
/// данные о болезнях живут только там. Арка срабатывает на входе, а не на выходе,
/// чтобы отклик совпадал с моментом, когда игрок шагнул под перекладину.
/// </remarks>
public abstract partial class SharedDiseaseSterilizerSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPowerReceiverSystem _power = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    [Dependency] private EntityQuery<MobStateComponent> _mobStateQuery = default!;
    [Dependency] private EntityQuery<StrapComponent> _strapQuery = default!;

    /// <summary>
    /// Без питания арка пропускает молча, а предметы и прочий мусор её не трогают.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnStepAttempt(Entity<DiseaseSterilizerComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue |= CanScan(ent, args.Tripper);
    }

    [SubscribeLocalEvent]
    private void OnStepTriggered(Entity<DiseaseSterilizerComponent> ent, ref StepTriggeredOnEvent args)
    {
        TryPass(ent, args.Tripper);
    }

    [SubscribeLocalEvent]
    private void OnExamined(Entity<DiseaseSterilizerComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("disease-sterilizer-examine",
            ("units", GetReagentUnits(ent).Int()),
            ("cost", ent.Comp.Cost.Int())));
    }

    /// <summary>
    /// Пропускает цель через арку: обдаёт дезинфектантом, если его хватает, и проверяет на заразу.
    /// </summary>
    /// <returns>True, если арка сработала.</returns>
    public bool TryPass(Entity<DiseaseSterilizerComponent> ent, EntityUid target)
    {
        if (!CanScan(ent, target))
            return false;

        var sprayed = TrySpray(ent, target);
        OnPassed(ent, target, sprayed);
        return true;
    }

    /// <summary>
    /// Реагирует ли арка на цель: она запитана, а цель — живое существо или каталка с пристёгнутым.
    /// </summary>
    public bool CanScan(Entity<DiseaseSterilizerComponent> ent, EntityUid target)
    {
        if (!_power.IsPowered(ent.Owner))
            return false;

        return _mobStateQuery.HasComp(target) ||
               _strapQuery.TryComp(target, out var strap) && strap.BuckledEntities.Count > 0;
    }

    /// <summary>
    /// Серверная часть прохода: обработка вещей и проверка на заразу.
    /// </summary>
    /// <param name="ent">Арка.</param>
    /// <param name="target">Прошедший.</param>
    /// <param name="sprayed">Хватило ли реагента на распыление.</param>
    protected virtual void OnPassed(Entity<DiseaseSterilizerComponent> ent, EntityUid target, bool sprayed)
    {
    }

    /// <summary>
    /// Переключает табло арки и заводит возврат в ожидание.
    /// </summary>
    protected void SetState(Entity<DiseaseSterilizerComponent> ent, DiseaseSterilizerState state)
    {
        _appearance.SetData(ent.Owner, DiseaseSterilizerVisuals.State, state);
        ent.Comp.StateResetTime = _timing.CurTime + ent.Comp.StateDuration;
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DiseaseSterilizerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.StateResetTime == TimeSpan.Zero || _timing.CurTime < comp.StateResetTime)
                continue;

            comp.StateResetTime = TimeSpan.Zero;
            _appearance.SetData(uid, DiseaseSterilizerVisuals.State, DiseaseSterilizerState.Idle);
            Dirty(uid, comp);
        }
    }

    /// <summary>
    /// Списывает порцию реагента и обдаёт цель. Звук и сообщение предсказываются для прошедшего.
    /// </summary>
    private bool TrySpray(Entity<DiseaseSterilizerComponent> ent, EntityUid target)
    {
        var reagent = new ReagentId(ent.Comp.Reagent.Id, null);
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out var solution, out var mixture) ||
            mixture.GetReagentQuantity(reagent) < ent.Comp.Cost)
        {
            return false;
        }

        _solution.RemoveReagent(solution.Value, reagent, ent.Comp.Cost);
        _audio.PlayPredicted(ent.Comp.SpraySound, ent.Owner, target);
        _popup.PopupEntity(Loc.GetString("disease-sterilizer-popup-sprayed"), target, target);
        SetState(ent, DiseaseSterilizerState.Spraying);
        return true;
    }

    private FixedPoint2 GetReagentUnits(Entity<DiseaseSterilizerComponent> ent)
    {
        if (!_solution.TryGetSolution(ent.Owner, ent.Comp.SolutionName, out _, out var solution))
            return FixedPoint2.Zero;

        return solution.GetReagentQuantity(new ReagentId(ent.Comp.Reagent.Id, null));
    }
}
