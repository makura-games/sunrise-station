using System.Linq;
using Content.Server.Nutrition.Components;
using Content.Shared._Sunrise.FleshCult;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Hands;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Player;

namespace Content.Server._Sunrise.FleshCult;

public sealed partial class FleshCultSystem
{
    [Dependency] private StatusEffectsSystem _huggerStatusEffects = default!;

    public void InitializeHugger()
    {
        SubscribeLocalEvent<FleshHuggerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FleshHuggerComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<FleshHuggerComponent, ThrowDoHitEvent>(OnWormDoHit);
        SubscribeLocalEvent<FleshHuggerComponent, GotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<FleshHuggerComponent, GotUnequippedEvent>(OnGotUnequipped);
        SubscribeLocalEvent<FleshHuggerComponent, GotEquippedHandEvent>(OnGotEquippedHand);
        SubscribeLocalEvent<FleshHuggerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FleshHuggerComponent, FleshHuggerJumpActionEvent>(OnJump);
        SubscribeLocalEvent<FleshHuggerComponent, FleshHuggerGetOffFromFaceActionEvent>(OnGetOff);
    }

    private void OnMapInit(EntityUid uid, FleshHuggerComponent component, MapInitEvent args)
    {
        _action.AddAction(uid, component.ActionFleshHuggerJumpId);
        _action.AddAction(uid, component.ActionFleshHuggerGetOffId);
    }

    private void OnWormDoHit(EntityUid uid, FleshHuggerComponent component, ThrowDoHitEvent args)
    {
        if (component.IsDeath)
            return;
        if (HasComp<FleshCultistComponent>(args.Target))
            return;
        if (!HasComp<HumanoidProfileComponent>(args.Target))
            return;
        if (TryComp(args.Target, out MobStateComponent? mobState))
        {
            if (mobState.CurrentState is not MobState.Alive)
            {
                return;
            }
        }
        _inventory.TryGetSlotEntity(args.Target, "head", out var headItem);
        if (HasComp<IngestionBlockerComponent>(headItem))
            return;

        _inventory.TryGetSlotEntity(args.Target, "mask", out var maskItem);
        if (HasComp<IdentityBlockerComponent>(maskItem))
            return;

        _inventory.TryUnequip(args.Target, "head", true);
        _inventory.TryUnequip(args.Target, "eyes", true);
        _inventory.TryUnequip(args.Target, "mask", true);

        var equipped = _inventory.TryEquip(args.Target, uid, "mask", true);
        if (!equipped)
            return;

        component.EquipedOn = args.Target;

        _popup.PopupEntity(Loc.GetString("flesh-pudge-throw-hugger-hit-user"),
            args.Target, args.Target, PopupType.LargeCaution);

        _popup.PopupEntity(Loc.GetString("flesh-pudge-throw-hugger-hit-mob",
                ("entity", args.Target)),
            uid, uid, PopupType.LargeCaution);

        _popup.PopupEntity(Loc.GetString("flesh-pudge-throw-hugger-eat-face-others",
            ("entity", args.Target)), args.Target, Filter.PvsExcept(uid), true, PopupType.Large);

        EnsureComp<PacifiedComponent>(uid);
        _stunSystem.TryAddParalyzeDuration(args.Target, TimeSpan.FromSeconds(component.ParalyzeTime));
        _damageableSystem.TryChangeDamage(args.Target, component.Damage, origin: args.Thrown);
    }

    private void OnGotEquipped(EntityUid uid, FleshHuggerComponent component, GotEquippedEvent args)
    {
        if (args.Slot != "mask")
            return;
        component.EquipedOn = args.EquipTarget;
        _huggerStatusEffects.TrySetStatusEffectDuration(args.EquipTarget, BlindnessSystem.BlindingStatusEffect);
        EnsureComp<PacifiedComponent>(uid);
    }

    private void OnGotEquippedHand(EntityUid uid, FleshHuggerComponent component, GotEquippedHandEvent args)
    {
        if (HasComp<FleshCultistComponent>(args.User))
            return;
        if (component.IsDeath)
            return;
        _damageableSystem.TryChangeDamage(args.User, component.Damage);
        _popup.PopupEntity(Loc.GetString("flesh-pudge-throw-hugger-bite-user"),
            args.User, args.User);
    }

    private void OnGotUnequipped(EntityUid uid, FleshHuggerComponent component, GotUnequippedEvent args)
    {
        if (args.Slot != "mask")
            return;
        if (HasComp<PacifiedComponent>(uid))
            RemComp<PacifiedComponent>(uid);
        _huggerStatusEffects.TryRemoveStatusEffect(args.EquipTarget, BlindnessSystem.BlindingStatusEffect);
        _stunSystem.TryAddParalyzeDuration(uid, TimeSpan.FromSeconds(3));
        component.EquipedOn = new EntityUid();
    }

    private void OnMeleeHit(EntityUid uid, FleshHuggerComponent component, MeleeHitEvent args)
    {
        if (!args.HitEntities.Any())
            return;

        foreach (var entity in args.HitEntities)
        {
            if (!HasComp<HumanoidProfileComponent>(entity))
                return;

            if (TryComp(entity, out MobStateComponent? mobState))
            {
                if (mobState.CurrentState is not MobState.Alive)
                {
                    return;
                }
            }

            _inventory.TryGetSlotEntity(entity, "head", out var headItem);
            if (HasComp<IngestionBlockerComponent>(headItem))
                return;

            _inventory.TryGetSlotEntity(entity, "mask", out var maskItem);
            if (HasComp<IdentityBlockerComponent>(maskItem))
                return;

            var random = new Random();
            var shouldEquip = random.Next(1, 101) <= component.ChansePounce;
            if (!shouldEquip)
                return;

            _inventory.TryUnequip(entity, "head", true);
            _inventory.TryUnequip(entity, "eyes", true);
            _inventory.TryUnequip(entity, "mask", true);

            var equipped = _inventory.TryEquip(entity, uid, "mask", true);
            if (!equipped)
                return;

            component.EquipedOn = entity;

            _popup.PopupEntity(Loc.GetString("flesh-pudge-throw-hugger-hit-user"),
                entity, entity, PopupType.LargeCaution);

            _popup.PopupEntity(Loc.GetString("flesh-pudge-throw-hugger-hit-mob", ("entity", entity)),
                uid, uid, PopupType.LargeCaution);

            _popup.PopupEntity(Loc.GetString("flesh-pudge-throw-hugger-eat-face-others",
                ("entity", entity)), entity, Filter.PvsExcept(entity), true, PopupType.Large);
            EnsureComp<PacifiedComponent>(uid);
            _stunSystem.TryAddParalyzeDuration(entity, TimeSpan.FromSeconds(component.ParalyzeTime));
            _damageableSystem.TryChangeDamage(entity, component.Damage, origin: entity);
            break;
        }
    }

    private static void OnMobStateChanged(EntityUid uid, FleshHuggerComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
        {
            component.IsDeath = true;
        }
    }

    private void OnGetOff(EntityUid uid, FleshHuggerComponent component, FleshHuggerGetOffFromFaceActionEvent args)
    {
        if (args.Handled)
            return;

        if (component.EquipedOn is not { Valid: true } targetId)
        {
            _popup.PopupEntity(Loc.GetString("flesh-worm-cant-get-off"),
                uid, uid, PopupType.LargeCaution);
            return;
        }

        _inventory.TryUnequip(targetId, "mask", true, true);
        component.EquipedOn = new EntityUid();

        args.Handled = true;
    }

    private void OnJump(EntityUid uid, FleshHuggerComponent component, FleshHuggerJumpActionEvent args)
    {
        if (args.Handled)
            return;

        if (component.EquipedOn is { Valid: true })
        {
            _popup.PopupEntity(Loc.GetString("flesh-worm-cant-jump"),
                uid, uid, PopupType.LargeCaution);
            return;
        }

        args.Handled = true;
        var xform = Transform(uid);
        var mapCoords = args.Target.ToMap(_entityManager, _transformSystem);
        var direction = mapCoords.Position - xform.MapPosition.Position;

        _throwing.TryThrow(uid, direction, 7F, uid, 10F);
        if (component.SoundJump != null)
        {
            _audioSystem.PlayPvs(component.SoundJump, uid, component.SoundJump.Params);
        }
    }

    public void UpdateHugger(float frameTime)
    {
        foreach (var comp in EntityQuery<FleshHuggerComponent>())
        {
            comp.Accumulator += frameTime;

            if (comp.Accumulator <= comp.DamageFrequency)
                continue;

            comp.Accumulator = 0;

            if (comp.EquipedOn is not { Valid: true } targetId)
                continue;
            if (HasComp<FleshCultistComponent>(comp.EquipedOn))
                return;
            if (TryComp(targetId, out MobStateComponent? mobState))
            {
                if (mobState.CurrentState is not MobState.Alive)
                {
                    _inventory.TryUnequip(targetId, "mask", true, true);
                    comp.EquipedOn = new EntityUid();
                    return;
                }
            }
            _damageableSystem.TryChangeDamage(targetId, comp.Damage);
            _popup.PopupEntity(Loc.GetString("flesh-pudge-throw-hugger-eat-face-user"),
                targetId, targetId, PopupType.LargeCaution);
            _popup.PopupEntity(Loc.GetString("flesh-pudge-throw-hugger-eat-face-others",
                ("entity", targetId)), targetId, Filter.PvsExcept(targetId), true);
        }
    }
}
