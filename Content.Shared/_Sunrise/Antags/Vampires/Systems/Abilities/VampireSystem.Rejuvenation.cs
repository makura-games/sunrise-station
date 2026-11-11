using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Metabolism;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    [SubscribeLocalEvent]
    public void OnRejuvenate(Entity<VampireComponent> ent, ref VampireRejuvenateActionEvent args)
    {
        if (!args.Handled)
            args.Handled = TryRejuvenate(ent.AsNullable(), args.Action);
    }

    public bool TryRejuvenate(Entity<VampireComponent?> ent, EntityUid action, bool quiet = false)
    {
        if (!Resolve(ent, ref ent.Comp, false) ||
            !CanUseVampireAction((ent, ent.Comp), action, out var configuration, out var level, out var bloodCost, quiet) ||
            !TrySpendBlood((ent, ent.Comp), bloodCost, showPopup: !quiet))
        {
            return false;
        }

        DoRejuvenate((ent, ent.Comp), configuration, level.Rejuvenation);
        return true;
    }

    private void DoRejuvenate(
        Entity<VampireComponent> ent,
        VampireConfigurationComponent configuration,
        VampireRejuvenationLevelSettings settings)
    {
        RemoveRejuvenateStuns(ent);
        _stamina.RestoreStamina((ent.Owner, null), settings.StaminaRestoreAmount);
        PurgeRejuvenateReagents(ent, settings.ReagentPurgeAmount, configuration.RejuvenatePurgeMetabolismStage);
        StartRejuvenateHealing(ent, settings);
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
            !_bloodstreamQuery.TryComp(uid, out var blood))
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

            if (!_prototype.TryIndex(
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
        Dirty(uid, active);
    }

    private void ProcessActiveRejuvenation(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ActiveVampireRejuvenateComponent, MetaDataComponent>();

        while (query.MoveNext(out var uid, out var rejuvenate, out var metadata))
        {
            if (metadata.EntityPaused || now < rejuvenate.NextApplication)
                continue;

            _damageable.TryChangeDamage(
                uid,
                rejuvenate.Healing,
                true);

            rejuvenate.ApplicationsRemaining--;
            DirtyField(uid, rejuvenate, nameof(ActiveVampireRejuvenateComponent.ApplicationsRemaining));

            if (rejuvenate.ApplicationsRemaining <= 0)
            {
                RemCompDeferred<ActiveVampireRejuvenateComponent>(uid);
                continue;
            }

            rejuvenate.NextApplication =
                now + rejuvenate.ApplicationInterval;
            DirtyField(uid, rejuvenate, nameof(ActiveVampireRejuvenateComponent.NextApplication));
        }
    }
}
