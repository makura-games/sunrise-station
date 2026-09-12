using Content.Server._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Events;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Metabolism;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    // Омоложение и его эффекты.

    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;


    private void OnRejuvenate(
        Entity<VampireComponent> ent,
        ref VampireRejuvenateIActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryRejuvenate(
            ent.AsNullable(),
            args.Action.Owner,
            upgraded: false);
    }

    private void OnRejuvenateUpgraded(
        Entity<VampireComponent> ent,
        ref VampireRejuvenateIiActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryRejuvenate(
            ent.AsNullable(),
            args.Action.Owner,
            upgraded: true);
    }


    public bool TryRejuvenate(
        Entity<VampireComponent?> ent,
        EntityUid action,
        bool upgraded)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (!CanRejuvenate(
                (ent.Owner, ent.Comp),
                action,
                upgraded))
        {
            return false;
        }

        if (!CheckAndConsumeBloodCost(
                (ent.Owner, ent.Comp),
                action))
        {
            return false;
        }

        DoRejuvenate(
            (ent.Owner, ent.Comp),
            upgraded);

        return true;
    }

    public bool CanRejuvenate(
        Entity<VampireComponent> ent,
        EntityUid action,
        bool upgraded,
        bool quiet = false)
    {
        if (!TryGetPowerLevelPrototype(ent.Comp.PowerLevel, out _))
            return false;

        if (upgraded &&
            !HasComp<VampireConfigurationComponent>(ent))
        {
            return false;
        }

        if (!TryResolveVampireActionCost(
                ent,
                action,
                0,
                out var bloodCost,
                showPopup: !quiet))
        {
            return false;
        }

        return CanSpendBlood(
            ent,
            bloodCost,
            showPopup: !quiet);
    }

    private void DoRejuvenate(
        Entity<VampireComponent> ent,
        bool upgraded)
    {
        if (!TryGetPowerLevelPrototype(ent.Comp.PowerLevel, out var level))
            return;

        var settings = level.Rejuvenation;

        RemoveRejuvenateStuns(ent.Owner);
        _stamina.RestoreStamina(
            ent.Owner,
            settings.StaminaRestoreAmount);

        if (!upgraded)
            return;

        if (!TryComp<VampireConfigurationComponent>(
                ent,
                out var configuration))
        {
            return;
        }

        PurgeRejuvenateReagents(
            ent.Owner,
            settings.ReagentPurgeAmount,
            configuration.RejuvenatePurgeMetabolismStage);

        StartRejuvenateHealing(
            ent.Owner,
            settings);
    }

    private void RemoveRejuvenateStuns(EntityUid uid)
    {
        _statusEffects.TryRemoveStatusEffect(
            uid,
            SharedStunSystem.StunId);

        _stun.TryUnstun(uid);
        RemComp<KnockedDownComponent>(uid);
    }

    private void PurgeRejuvenateReagents(
        EntityUid uid,
        float configuredAmount,
        ProtoId<MetabolismStagePrototype> metabolismStage)
    {
        var purgeAmount = FixedPoint2.New(configuredAmount);

        if (purgeAmount <= FixedPoint2.Zero ||
            !TryComp<BloodstreamComponent>(uid, out var blood))
        {
            return;
        }

        if (!_solution.ResolveSolution(
                uid,
                blood.BloodSolutionName,
                ref blood.BloodSolution,
                out var bloodstreamSolution))
        {
            return;
        }

        var solutionEntity = blood.BloodSolution.Value;
        var removed = FixedPoint2.Zero;

        foreach (var quantity in bloodstreamSolution.Contents.ToArray())
        {
            if (removed >= purgeAmount)
                break;

            if (!_prototype.TryIndex<ReagentPrototype>(
                    quantity.Reagent.Prototype,
                    out var prototype) ||
                prototype.Metabolisms is null ||
                !prototype.Metabolisms.Metabolisms.ContainsKey(metabolismStage))
            {
                continue;
            }

            var removeAmount = FixedPoint2.Min(
                quantity.Quantity,
                purgeAmount - removed);

            _solution.RemoveReagent(
                solutionEntity,
                quantity.Reagent,
                removeAmount);

            removed += removeAmount;
        }
    }

    private void StartRejuvenateHealing(
        EntityUid uid,
        VampireRejuvenationLevelSettings settings)
    {
        if (settings.HealApplications <= 0 ||
            settings.Healing.Empty)
        {
            return;
        }

        var active = EnsureComp<ActiveVampireRejuvenateComponent>(uid);

        active.ApplicationsRemaining = settings.HealApplications;
        active.ApplicationInterval = settings.HealInterval;
        active.NextApplication = _timing.CurTime;
        active.Healing = new DamageSpecifier(settings.Healing);
    }

    private void ProcessActiveRejuvenation(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ActiveVampireRejuvenateComponent>();

        while (query.MoveNext(out var uid, out var rejuvenate))
        {
            if (now < rejuvenate.NextApplication)
                continue;

            _damageable.TryChangeDamage(
                uid,
                rejuvenate.Healing,
                true);

            rejuvenate.ApplicationsRemaining--;

            if (rejuvenate.ApplicationsRemaining <= 0)
            {
                RemCompDeferred<ActiveVampireRejuvenateComponent>(uid);
                continue;
            }

            rejuvenate.NextApplication =
                now + rejuvenate.ApplicationInterval;
        }
    }
}
