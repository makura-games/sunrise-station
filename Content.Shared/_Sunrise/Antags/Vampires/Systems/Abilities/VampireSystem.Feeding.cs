using System.Numerics;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Events;
using Content.Shared._Sunrise.Body.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;

namespace Content.Shared._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    // Укусы и питание кровью.

    [Dependency] private BloodstreamSystem _blood = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private EntityQuery<BloodstreamComponent> _bloodstreamQuery = default!;
    [Dependency] private EntityQuery<BloodSourceComponent> _bloodSourceQuery = default!;
    [Dependency] private EntityQuery<BlindableComponent> _blindableQuery = default!;

    [SubscribeLocalEvent]
    public void OnToggleFangs(Entity<VampireComponent> ent, ref VampireToggleFangsActionEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.FangsExtended = !ent.Comp.FangsExtended;
        if (!ent.Comp.FangsExtended &&
            _feedingQuery.TryComp(ent, out var feeding) &&
            feeding.DrinkDoAfter is { } doAfter)
        {
            _doAfter.Cancel(ent, doAfter);
        }

        _actions.SetToggled(args.Action.AsNullable(), ent.Comp.FangsExtended);

        DirtyField(ent, ent.Comp, nameof(VampireComponent.FangsExtended));
        args.Handled = true;
    }

    [SubscribeLocalEvent]
    public void OnDrinkDoAfter(Entity<VampireComponent> ent, ref VampireDrinkBloodDoAfterEvent args)
    {
        if (args.Handled || !_feedingQuery.TryComp(ent, out var feeding))
            return;

        args.Handled = true;
        feeding.DrinkDoAfter = null;
        Dirty(ent, feeding);

        if (args.Cancelled ||
            !ent.Comp.FangsExtended ||
            IsMouthBlocked(ent, feeding) ||
            args.Args.Target is not { } targetUid ||
            !_bloodstreamQuery.TryComp(targetUid, out var blood) ||
            !_bloodSourceQuery.TryComp(targetUid, out var bloodSource) ||
            bloodSource.Value <= 0f ||
            IsInvalidDrinkTarget(ent.Owner, targetUid, showPopup: false))
        {
            return;
        }

        var drunkFromTarget = feeding.BloodDrunkFromTargets.GetValueOrDefault(targetUid);
        if (drunkFromTarget >= feeding.MaxBloodPerTarget)
        {
            _popup.PopupEntity(
                Loc.GetString("vampire-drink-target-maxed", ("amount", feeding.MaxBloodPerTarget)),
                ent.Owner,
                ent.Owner,
                PopupType.MediumCaution);
            return;
        }

        var bloodEfficiency = bloodSource.Value;

        if (TryComp<MobStateComponent>(targetUid, out var mobState) &&
            mobState.CurrentState == Content.Shared.Mobs.MobState.Dead)
        {
            bloodEfficiency *= bloodSource.CorpseMultiplier;
        }

        if (TryComp<PerishableComponent>(targetUid, out var rot))
        {
            var stage = Math.Clamp(rot.Stage, 0, 4);
            bloodEfficiency *= bloodSource.RotMultipliers.GetValueOrDefault(stage);
        }

        if (bloodEfficiency <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-rot"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            return;
        }

        var maxCanDrink = feeding.MaxBloodPerTarget - drunkFromTarget;
        var fullSipGain = feeding.BloodGainPerSip * bloodEfficiency;
        var cappedSipGain = MathF.Min(fullSipGain, maxCanDrink);
        if (cappedSipGain <= 0f || feeding.TargetBloodDrainPerSip <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-empty"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            return;
        }

        var targetBloodLevel =
            _blood.GetBloodLevel((targetUid, blood)) * (float) blood.BloodReferenceSolution.Volume;
        if (targetBloodLevel <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-empty"),
                ent.Owner,
                ent.Owner,
                PopupType.MediumCaution);
            return;
        }

        var intendedDrain = feeding.TargetBloodDrainPerSip * (cappedSipGain / fullSipGain);
        var actualDrain = MathF.Min(intendedDrain, targetBloodLevel);
        var actualSipGain = cappedSipGain * (actualDrain / intendedDrain);

        if (!_blood.TryModifyBloodLevel(targetUid, -actualDrain))
        {
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-empty"),
                ent.Owner,
                ent.Owner,
                PopupType.MediumCaution);
            return;
        }

        // BloodstreamSystem меняет состав напрямую; уведомляем систему растворов для синхронизации и отката.
        _solution.UpdateChemicals(blood.BloodSolution!.Value, needsReactionsProcessing: false);
        AddBlood(ent, actualSipGain, targetUid);

        _damageable.TryChangeDamage(targetUid, feeding.BiteDamage, ignoreResistances: true);
        _blood.TryModifyBleedAmount(targetUid, feeding.BiteBleedAmount);

        if (_blindableQuery.TryComp(targetUid, out var blindable))
        {
            var biteCount = feeding.BiteCountsByTarget.GetValueOrDefault(targetUid) + 1;
            if (biteCount >= feeding.BitesPerEyeDamage)
            {
                _blindable.AdjustEyeDamage((targetUid, blindable), feeding.EyeDamage);
                biteCount = 0;
            }

            feeding.BiteCountsByTarget[targetUid] = biteCount;
        }

        var healingScale = actualSipGain / feeding.BloodGainPerSip;
        _damageable.TryChangeDamage(ent.Owner, feeding.Healing * healingScale, true);

        _audio.PlayPredicted(feeding.BiteSound, targetUid, ent, AudioParams.Default.WithVolume(feeding.BiteVolume));
        var xform = Transform(ent);
        var localPosition = Vector2.Transform(_transform.GetWorldPosition(targetUid), _transform.GetInvWorldMatrix(xform));
        localPosition = xform.LocalRotation.RotateVec(localPosition);
        _melee.DoLunge(ent, ent, Angle.Zero, localPosition, feeding.BiteEffect);

        var currentDrunkFromTarget = feeding.BloodDrunkFromTargets.GetValueOrDefault(targetUid);

        if (ent.Comp.FangsExtended && currentDrunkFromTarget < feeding.MaxBloodPerTarget)
        {
            // Повторяем тот же DoAfter, чтобы его ID и состояние одинаково откатывались на клиенте и сервере.
            args.Repeat = true;
            feeding.DrinkDoAfter = args.DoAfter.Index;
            return;
        }

        if (currentDrunkFromTarget >= feeding.MaxBloodPerTarget)
        {
            _popup.PopupEntity(
                Loc.GetString("vampire-drink-target-hard-max", ("amount", feeding.MaxBloodPerTarget)),
                ent.Owner,
                ent.Owner,
                PopupType.MediumCaution);
        }
    }

    [SubscribeLocalEvent]
    public void OnBloodSourceTerminating(Entity<BloodSourceComponent> ent, ref EntityTerminatingEvent args)
    {
        // Удалённую сущность нельзя преобразовать в NetEntity при следующем снимке истории укусов.
        var query = EntityQueryEnumerator<VampireFeedingComponent>();
        while (query.MoveNext(out var uid, out var feeding))
        {
            var changed = feeding.BloodDrunkFromTargets.Remove(ent);
            changed |= feeding.BiteCountsByTarget.Remove(ent);
            if (changed && !TerminatingOrDeleted(uid))
                Dirty(uid, feeding);
        }
    }

    private bool TryStartDrinkBlood(Entity<VampireComponent> ent, EntityUid target)
    {
        if (!_feedingQuery.TryComp(ent, out var feeding) ||
            !_configurationQuery.TryComp(ent, out var configuration) ||
            feeding.DrinkDoAfter != null ||
            !CanDrinkBlood(ent, target))
        {
            return false;
        }

        if (IsInvalidDrinkTarget(ent.Owner, target))
            return false;

        if (IsProtectedByFaith(target) && ent.Comp.PowerLevel < configuration.FaithProtectionPowerLevel)
        {
            _popup.PopupEntity(
                Loc.GetString("vampire-target-protected-by-faith"),
                ent.Owner,
                ent.Owner,
                PopupType.MediumCaution);
            return false;
        }

        if (!IsMouthBlocked(ent.Owner, feeding))
            return StartDrinkDoAfter((ent, feeding), target);

        _popup.PopupEntity(Loc.GetString("vampire-mouth-covered"), ent.Owner, ent.Owner);
        return false;
    }

    private bool IsInvalidDrinkTarget(EntityUid user, EntityUid target, bool showPopup = true)
    {
        if (!HasComp<VampireComponent>(target))
            return false;

        if (showPopup)
        {
            _popup.PopupEntity(Loc.GetString("vampire-drink-invalid-target"),
                user,
                user,
                PopupType.MediumCaution);
        }

        return true;
    }

    private bool StartDrinkDoAfter(Entity<VampireFeedingComponent> ent, EntityUid target)
    {
        var feeding = ent.Comp;
        var doAfter = new DoAfterArgs(
            EntityManager,
            ent.Owner,
            feeding.SipInterval,
            new VampireDrinkBloodDoAfterEvent(),
            ent.Owner,
            target)
        {
            DistanceThreshold = feeding.BiteDistanceThreshold,
            NeedHand = true,
            NeedFreeHand = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
            BlockDuplicate = true,
            CancelDuplicate = false,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter, out var doAfterId))
            return false;

        feeding.DrinkDoAfter = _doAfter.IsRunning(doAfterId) ? doAfterId.Value.Index : null;
        Dirty(ent);
        _popup.PopupEntity(
            Loc.GetString("vampire-drink-start", ("target", Identity.Entity(target, EntityManager))),
            ent.Owner,
            ent.Owner);

        return true;
    }

    private bool IsMouthBlocked(EntityUid uid, VampireFeedingComponent feeding)
    {
        if (!HasComp<InventoryComponent>(uid))
            return false;

        foreach (var slot in feeding.MouthCoveringSlots)
        {
            if (_inventory.TryGetSlotEntity(uid, slot, out var item) &&
                TryComp<IngestionBlockerComponent>(item.Value, out var blocker) &&
                blocker.Enabled)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateVampireAlert(Entity<VampireComponent> ent)
        => _alerts.ShowAlert(ent.Owner, ent.Comp.BloodAlert);

    private void UpdateVampireFedAlert(Entity<VampireComponent> ent)
    {
        if (!_configurationQuery.TryComp(ent, out var configuration) ||
            !_feedingQuery.TryComp(ent, out var feeding))
        {
            return;
        }

        var fraction = feeding.MaxBloodFullness <= 0f
            ? 0f
            : ent.Comp.BloodFullness / feeding.MaxBloodFullness;
        var minSeverity = _alerts.GetMinSeverity(configuration.FedAlert);
        var maxSeverity = _alerts.GetMaxSeverity(configuration.FedAlert);
        var severity = (short)Math.Clamp(
            (int)MathF.Ceiling(fraction * (maxSeverity - minSeverity)) + minSeverity,
            minSeverity,
            maxSeverity);
        _alerts.ShowAlert(ent.Owner, configuration.FedAlert, severity);
    }
}
