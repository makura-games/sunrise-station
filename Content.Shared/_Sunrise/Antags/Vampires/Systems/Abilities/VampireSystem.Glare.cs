using System.Diagnostics.CodeAnalysis;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Stunnable;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Events;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Shared._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityQuery<StaminaComponent> _staminaQuery = default!;
    [Dependency] private EntityQuery<KnockedDownComponent> _knockedDownQuery = default!;

    [SubscribeLocalEvent]
    public void OnGlare(Entity<VampireComponent> ent, ref VampireGlareActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryGlare(
            ent.AsNullable(),
            args.Action);
    }

    public bool TryGlare(Entity<VampireComponent?> ent, EntityUid action, bool quiet = false)
    {
        if (!Resolve(ent, ref ent.Comp, false) ||
            !CanGlare((ent, ent.Comp), action, out var configuration, out var level, out var bloodCost, quiet) ||
            !TrySpendBlood((ent, ent.Comp), bloodCost, showPopup: !quiet))
        {
            return false;
        }

        DoGlare((ent, ent.Comp), configuration, level.Glare);
        return true;
    }

    public bool CanGlare(
        Entity<VampireComponent> ent,
        EntityUid action,
        [NotNullWhen(true)] out VampireConfigurationComponent? configuration,
        out VampirePowerLevelPrototype level,
        out int bloodCost,
        bool quiet = false)
    {
        configuration = null;
        level = default!;
        bloodCost = 0;
        return !(_blindableQuery.TryComp(ent, out var blindable) && blindable.IsBlind) &&
            CanUseVampireAction(ent, action, out configuration, out level, out bloodCost, quiet);
    }

    private void DoGlare(
        Entity<VampireComponent> ent,
        VampireConfigurationComponent configuration,
        VampireGlareLevelSettings settings)
    {
        var targets = _lookup.GetEntitiesInRange(
            ent.Owner,
            settings.Range,
            LookupFlags.Dynamic | LookupFlags.Sundries);

        var (ourPosition, ourRotation) =
            _transform.GetWorldPositionRotation(Transform(ent));

        var ourDirection = ourRotation.ToWorldVec();

        foreach (var target in targets)
        {
            if (target == ent.Owner || !_staminaQuery.TryComp(target, out var stamina))
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

            var knockedDown = _knockedDownQuery.HasComp(target);
            var inFront = dot > configuration.GlareDirectionThreshold && !knockedDown;

            if (inFront)
            {
                _stun.TryAddParalyzeDuration(
                    target,
                    settings.FrontParalyzeDuration * effectScale);
            }
            else if (dot >= -configuration.GlareDirectionThreshold || knockedDown)
            {
                _stun.TryAddParalyzeDuration(
                    target,
                    settings.SideParalyzeDuration * effectScale);
            }

            _stamina.TakeStaminaDamage(
                target,
                settings.StaminaDamage * effectScale,
                stamina,
                source: ent.Owner);

            if (inFront)
            {
                TryInjectMuteToxin(
                    target,
                    settings.MuteToxinAmount * effectScale,
                    configuration.MuteToxinReagent);
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

        if (!_solution.TryGetInjectableSolution(
                target,
                out var targetSolution,
                out _))
        {
            return false;
        }

        return _solution.TryAddReagent(
            targetSolution.Value,
            reagent,
            FixedPoint2.New(amount));
    }
}
