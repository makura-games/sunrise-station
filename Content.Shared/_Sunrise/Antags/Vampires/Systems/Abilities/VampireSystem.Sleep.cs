using Content.Shared.Bed.Sleep;
using Content.Shared.DoAfter;
using Content.Shared.Mindshield.Components;
using Content.Shared.Popups;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Events;
using Content.Shared._Sunrise.Random;

namespace Content.Shared._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    // Заряд тратится при старте, кровь — после успешной концентрации.

    [Dependency] private RandomPredictedSystem _random = default!;

    [SubscribeLocalEvent]
    public void OnSleep(Entity<VampireComponent> ent, ref VampireSleepActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TrySleep(
            ent.AsNullable(),
            args.Target,
            args.Action.Owner);
    }

    public bool TrySleep(
        Entity<VampireComponent?> ent,
        EntityUid target,
        EntityUid action,
        bool quiet = false)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        Entity<VampireComponent> vampire = (ent.Owner, ent.Comp);

        if (!CanSleep(vampire, target, quiet))
            return false;

        return DoSleep(vampire, target, action);
    }

    public bool CanSleep(
        Entity<VampireComponent> ent,
        EntityUid target,
        bool quiet = false)
    {
        if (!Exists(target))
            return false;

        if (!TryGetPowerLevelPrototype(ent.Comp.PowerLevel, out var level))
            return false;

        var settings = level.Sleep;

        if (target == ent.Owner ||
            !_interaction.InRangeAndAccessible(ent.Owner, target, settings.TargetRange))
        {
            return false;
        }

        return CanAffectSleepTarget(ent, target, settings.IgnoresFaith, quiet) &&
            CanSpendBlood(ent, settings.BloodCost, showPopup: !quiet);
    }

    private bool CanAffectSleepTarget(
        Entity<VampireComponent> ent,
        EntityUid target,
        bool ignoresFaith,
        bool quiet = false)
    {
        if (!Exists(target))
            return false;

        if (IsProtectedByFaith(target) && !ignoresFaith)
        {
            if (!quiet)
            {
                _popup.PopupEntity(
                    Loc.GetString("vampire-target-protected-by-faith"),
                    ent.Owner,
                    ent.Owner,
                    PopupType.MediumCaution);
            }

            return false;
        }

        if (HasFlashProtection(target))
        {
            if (!quiet)
            {
                _popup.PopupEntity(
                    Loc.GetString("vampire-sleep-protected"),
                    ent.Owner,
                    ent.Owner,
                    PopupType.MediumCaution);
            }

            return false;
        }

        if (!HasComp<MindShieldComponent>(target))
            return true;

        if (!quiet)
        {
            _popup.PopupEntity(
                Loc.GetString("vampire-sleep-shielded"),
                ent.Owner,
                ent.Owner,
                PopupType.SmallCaution);
        }

        return false;
    }

    private bool DoSleep(
        Entity<VampireComponent> ent,
        EntityUid target,
        EntityUid action)
    {
        if (!_configurationQuery.TryComp(ent, out var configuration))
            return false;

        if (!TryGetPowerLevelPrototype(ent.Comp.PowerLevel, out var level))
            return false;

        var settings = level.Sleep;

        var doAfterEvent = new VampireSleepDoAfterEvent
        {
            Victim = GetNetEntity(target),
            Action = GetNetEntity(action),
            MaxDistance = settings.BreakRange,
            BloodCost = settings.BloodCost,
            Duration = settings.Duration,
            IgnoresFaith = settings.IgnoresFaith,
        };

        var doAfter = new DoAfterArgs(
            EntityManager,
            ent.Owner,
            settings.ChannelTime,
            doAfterEvent,
            ent.Owner)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
            MovementThreshold = configuration.SleepMovementThreshold,
            RequireCanInteract = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
            Hidden = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(
            Loc.GetString(_random.PickForEntity(ent, configuration.SleepTargetMessages)),
            target,
            target,
            PopupType.SmallCaution);

        return true;
    }

    [SubscribeLocalEvent]
    public void OnSleepDoAfterAttempt(
        Entity<VampireComponent> ent,
        ref DoAfterAttemptEvent<VampireSleepDoAfterEvent> args)
    {
        if (!TryGetEntity(args.Event.Victim, out var target) ||
            !Exists(target) ||
            !_interaction.InRangeAndAccessible(ent.Owner, target.Value, args.Event.MaxDistance))
        {
            args.Cancel();
        }
    }

    [SubscribeLocalEvent]
    public void OnSleepDoAfter(
        Entity<VampireComponent> ent,
        ref VampireSleepDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled ||
            !TryGetEntity(args.Victim, out var target) ||
            !CanAffectSleepTarget(ent, target.Value, args.IgnoresFaith) ||
            !TrySpendBlood(ent, args.BloodCost))
        {
            RefundSleepAction(args.Action);
            return;
        }

        _statusEffects.TryAddStatusEffectDuration(
            target.Value,
            SleepingSystem.StatusEffectForcedSleeping,
            args.Duration);
    }

    private void RefundSleepAction(NetEntity netAction)
    {
        if (!TryGetEntity(netAction, out var action) ||
            !_chargesQuery.TryComp(action, out var charges))
        {
            return;
        }

        _rechargeQuery.TryComp(action, out var recharge);
        _charges.AddCharges((action.Value, charges, recharge), 1);
        _actions.ClearCooldown(action.Value);
    }
}
