using Content.Server.Body.Systems;
using Content.Server.DoAfter;
using Content.Server._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Events;
using Content.Shared._Sunrise.Body.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Body.Components;
using Content.Shared.DoAfter;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Server.Audio;

namespace Content.Server._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    // Укусы и питание кровью.

    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly BlindableSystem _blindable = default!;


    private void OnToggleFangs(Entity<VampireComponent> ent, ref VampireToggleFangsActionEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.FangsExtended = !ent.Comp.FangsExtended;
        if (!ent.Comp.FangsExtended && TryComp<VampireFeedingComponent>(ent, out var feeding))
            feeding.IsDrinking = false;

        _actions.SetToggled(args.Action.AsNullable(), ent.Comp.FangsExtended);

        DirtyField(ent, ent.Comp, nameof(VampireComponent.FangsExtended));
        args.Handled = true;
    }

    private void OnAfterInteract(Entity<VampireComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !ent.Comp.FangsExtended || !Exists(args.Target))
            return;

        var target = args.Target.Value;
        if (target == ent.Owner || !HasComp<BloodstreamComponent>(target))
            return;

        if (TryStartDrinkBlood(ent, target))
            args.Handled = true;
    }

    private void OnDrinkDoAfter(Entity<VampireComponent> ent, ref VampireDrinkBloodDoAfterEvent args)
    {
        if (args.Handled || !TryComp<VampireFeedingComponent>(ent, out var feeding))
            return;

        if (args.Cancelled ||
            !ent.Comp.FangsExtended ||
            args.Args.Target is not { } targetUid ||
            !HasComp<BloodstreamComponent>(targetUid) ||
            !TryComp<BloodSourceComponent>(targetUid, out var bloodSource) ||
            bloodSource.Value <= 0f ||
            IsInvalidDrinkTarget(ent.Owner, targetUid, showPopup: false))
        {
            feeding.IsDrinking = false;
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
            feeding.IsDrinking = false;
            return;
        }

        var bloodEfficiency = bloodSource.Value;

        if (TryComp<MobStateComponent>(targetUid, out var mobState) &&
            mobState.CurrentState == Shared.Mobs.MobState.Dead)
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
            feeding.IsDrinking = false;
            return;
        }

        var maxCanDrink = feeding.MaxBloodPerTarget - drunkFromTarget;
        var fullSipGain = feeding.BloodGainPerSip * bloodEfficiency;
        var cappedSipGain = MathF.Min(fullSipGain, maxCanDrink);
        if (cappedSipGain <= 0f ||
            feeding.TargetBloodDrainPerSip <= 0f ||
            !TryComp<BloodstreamComponent>(targetUid, out var blood))
        {
            feeding.IsDrinking = false;
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-empty"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            return;
        }

        var targetBloodLevel =
            _blood.GetBloodLevel(targetUid) * blood.BloodReferenceSolution.MaxVolume.Value / 100;
        if (targetBloodLevel <= 0f)
        {
            feeding.IsDrinking = false;
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
            feeding.IsDrinking = false;
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-empty"),
                ent.Owner,
                ent.Owner,
                PopupType.MediumCaution);
            return;
        }

        AddBlood(ent, actualSipGain, targetUid);

        _damageable.TryChangeDamage(targetUid, feeding.BiteDamage, ignoreResistances: true);
        _blood.TryModifyBleedAmount(targetUid, feeding.BiteBleedAmount);

        if (TryComp<BlindableComponent>(targetUid, out var blindable))
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

        _audio.PlayPvs(feeding.BiteSound, targetUid, AudioParams.Default.WithVolume(feeding.BiteVolume));
        Spawn(feeding.BiteEffect, Transform(targetUid).Coordinates);

        var currentDrunkFromTarget = feeding.BloodDrunkFromTargets.GetValueOrDefault(targetUid);
        feeding.IsDrinking = false;

        if (ent.Comp.FangsExtended && currentDrunkFromTarget < feeding.MaxBloodPerTarget)
        {
            StartDrinkDoAfter(ent, targetUid, showPopup: false);
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

    protected override bool TryStartDrinkBlood(Entity<VampireComponent> ent, EntityUid target)
    {
        if (!base.TryStartDrinkBlood(ent, target))
            return false;

        if (!TryComp<VampireFeedingComponent>(ent, out var feeding) ||
            !TryComp<VampireConfigurationComponent>(ent, out var configuration))
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
            return StartDrinkDoAfter(ent, target, showPopup: true);

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

    private bool StartDrinkDoAfter(Entity<VampireComponent> ent, EntityUid target, bool showPopup)
    {
        if (!TryComp<VampireFeedingComponent>(ent, out var feeding) || feeding.IsDrinking)
            return false;

        if (IsMouthBlocked(ent.Owner, feeding))
        {
            if (showPopup)
            {
                _popup.PopupEntity(Loc.GetString("vampire-mouth-covered"),
                    ent.Owner,
                    ent.Owner);
            }

            return false;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            ent.Owner,
            feeding.SipInterval,
            new VampireDrinkBloodDoAfterEvent(),
            ent.Owner,
            target)
        {
            DistanceThreshold = feeding.BiteDistanceThreshold,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            AttemptFrequency = AttemptFrequency.StartAndEnd,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        feeding.IsDrinking = true;
        if (showPopup)
        {
            _popup.PopupEntity(
                Loc.GetString("vampire-drink-start", ("target", Identity.Entity(target, EntityManager))),
                ent.Owner,
                ent.Owner);
        }

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
        if (!TryComp<VampireConfigurationComponent>(ent, out var configuration) ||
            !TryComp<VampireFeedingComponent>(ent, out var feeding))
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
