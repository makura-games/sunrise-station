using System.Numerics;
using Content.Server._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Events;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    // Вампирский взгляд.

    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private void OnGlare(Entity<VampireComponent> ent, ref VampireGlareActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryGlare(
            ent.AsNullable(),
            args.Action);
    }

    /// <summary>
    /// Пытается применить вампирский взгляд.
    /// </summary>
    public bool TryGlare(
        Entity<VampireComponent?> ent,
        EntityUid action,
        bool quiet = false)
    {
        if (!CanGlare(ent, action, quiet))
            return false;

        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        var vampire = new Entity<VampireComponent>(ent.Owner, ent.Comp);

        if (!CheckAndConsumeBloodCost(vampire, action))
            return false;

        DoGlare(vampire);
        return true;
    }

    /// <summary>
    /// Проверяет, может ли вампир применить вампирский взгляд.
    /// </summary>
    public bool CanGlare(
        Entity<VampireComponent?> ent,
        EntityUid action,
        bool quiet = false)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (TryComp<BlindableComponent>(ent.Owner, out var blindable) &&
            blindable.IsBlind)
        {
            return false;
        }

        if (!TryGetPowerLevelPrototype(ent.Comp.PowerLevel, out _))
            return false;

        if (!HasComp<VampireConfigurationComponent>(ent.Owner))
            return false;

        var vampire = new Entity<VampireComponent>(ent.Owner, ent.Comp);

        if (!TryResolveVampireActionCost(
                vampire,
                action,
                0,
                out var bloodCost,
                showPopup: !quiet))
        {
            return false;
        }

        return CanSpendBlood(
            vampire,
            bloodCost,
            showPopup: !quiet);
    }

    private void DoGlare(Entity<VampireComponent> ent)
    {
        if (!TryGetPowerLevelPrototype(ent.Comp.PowerLevel, out var level))
            return;

        if (!TryComp<VampireConfigurationComponent>(ent, out var configuration))
            return;

        var settings = level.Glare;
        var targets = _lookup.GetEntitiesInRange(
            ent.Owner,
            settings.Range,
            LookupFlags.Dynamic | LookupFlags.Sundries);

        var (ourPosition, ourRotation) =
            _transform.GetWorldPositionRotation(Transform(ent));

        var ourDirection = ourRotation.ToWorldVec();

        foreach (var target in targets)
        {
            if (target == ent.Owner)
                continue;

            var effectScale = HasFlashProtection(target)
                ? settings.FlashProtectionEffectScale
                : 1f;

            if (effectScale <= 0f)
                continue;

            var offset = _transform.GetWorldPosition(target) - ourPosition;
            var dot = offset.LengthSquared() > 0f
                ? Vector2.Dot(ourDirection, Vector2.Normalize(offset))
                : 0f;

            if (!TryComp<StaminaComponent>(target, out var stamina))
                continue;

            var knockedDown = HasComp<KnockedDownComponent>(target);

            if (dot > configuration.GlareDirectionThreshold && !knockedDown)
            {
                _stun.TryAddParalyzeDuration(
                    target,
                    settings.FrontParalyzeDuration * effectScale);

                _stamina.TakeStaminaDamage(
                    target,
                    settings.StaminaDamage * effectScale,
                    stamina,
                    source: ent.Owner);

                TryInjectMuteToxin(
                    target,
                    settings.MuteToxinAmount * effectScale,
                    configuration.MuteToxinReagent);
            }
            else if (dot < -configuration.GlareDirectionThreshold && !knockedDown)
            {
                _stamina.TakeStaminaDamage(
                    target,
                    settings.StaminaDamage * effectScale,
                    stamina,
                    source: ent.Owner);
            }
            else
            {
                _stun.TryAddParalyzeDuration(
                    target,
                    settings.SideParalyzeDuration * effectScale);

                _stamina.TakeStaminaDamage(
                    target,
                    settings.StaminaDamage * effectScale,
                    stamina,
                    source: ent.Owner);
            }
        }
    }

    private bool TryInjectMuteToxin(
        EntityUid target,
        float amount,
        ProtoId<ReagentPrototype> reagent)
    {
        if (amount <= 0f)
            return false;

        var solution = new Solution();
        solution.AddReagent(reagent, FixedPoint2.New(amount));

        if (!_solution.TryGetInjectableSolution(
                target,
                out var targetSolution,
                out _))
        {
            return false;
        }

        return _solution.TryAddSolution(
            targetSolution.Value,
            solution);
    }
}
