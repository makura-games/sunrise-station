using Content.Server._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Events;
using Content.Shared.Bed.Sleep;
using Content.Shared.Charges.Components;
using Content.Shared.DoAfter;
using Content.Shared.Mindshield.Components;
using Content.Shared.Popups;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    // Усыпление цели.

    [Dependency] private readonly IRobustRandom _random = default!;


    private void OnSleep(Entity<VampireComponent> ent, ref VampireSleepActionEvent args)
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

        if (IsProtectedByFaith(target) && !settings.IgnoresFaith)
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
            return CanSpendBlood(ent, settings.BloodCost, showPopup: !quiet);


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
        if (!TryComp<VampireConfigurationComponent>(ent, out var configuration))
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
            Loc.GetString(_random.Pick(configuration.SleepTargetMessages)),
            target,
            target,
            PopupType.SmallCaution);

        return true;
    }

    private void OnSleepDoAfterAttempt(
        Entity<VampireComponent> ent,
        ref DoAfterAttemptEvent<VampireSleepDoAfterEvent> args)
    {
        var target = GetEntity(args.Event.Victim);

        if (!Exists(target) ||
            !_interaction.InRangeAndAccessible(ent.Owner, target, args.Event.MaxDistance))
        {
            args.Cancel();
        }
    }

    private void OnSleepDoAfter(
        Entity<VampireComponent> ent,
        ref VampireSleepDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (args.Cancelled)
        {
            RefundSleepAction(args.Action);
            args.Handled = true;
            return;
        }

        var target = GetEntity(args.Victim);

        if (!Exists(target))
        {
            RefundSleepAction(args.Action);
            args.Handled = true;
            return;
        }

        if (IsProtectedByFaith(target) && !args.IgnoresFaith)
        {
            _popup.PopupEntity(
                Loc.GetString("vampire-target-protected-by-faith"),
                ent.Owner,
                ent.Owner,
                PopupType.MediumCaution);

            RefundSleepAction(args.Action);
            args.Handled = true;
            return;
        }

        if (HasFlashProtection(target))
        {
            _popup.PopupEntity(
                Loc.GetString("vampire-sleep-protected"),
                ent.Owner,
                ent.Owner,
                PopupType.MediumCaution);

            RefundSleepAction(args.Action);
            args.Handled = true;
            return;
        }

        if (HasComp<MindShieldComponent>(target))
        {
            _popup.PopupEntity(
                Loc.GetString("vampire-sleep-shielded"),
                ent.Owner,
                ent.Owner,
                PopupType.SmallCaution);

            RefundSleepAction(args.Action);
            args.Handled = true;
            return;
        }

        if (!CheckAndConsumeBloodCost(ent, bloodCost: args.BloodCost))
        {
            RefundSleepAction(args.Action);
            args.Handled = true;
            return;
        }

        _statusEffects.TryAddStatusEffectDuration(
            target,
            SleepingSystem.StatusEffectForcedSleeping,
            args.Duration);

        args.Handled = true;
    }

    private void RefundSleepAction(NetEntity netAction)
    {
        var action = GetEntity(netAction);

        if (!Exists(action) ||
            !TryComp<LimitedChargesComponent>(action, out var charges))
        {
            return;
        }

        TryComp<AutoRechargeComponent>(action, out var recharge);
        _charges.AddCharges((action, charges, recharge), 1);
        _actions.ClearCooldown(action);
    }
}
