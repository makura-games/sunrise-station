using Content.Server.Bible.Components;
using Content.Shared._Sunrise.Antags.Vampires;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Prototypes;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Nutrition.Components;
using Content.Shared.Physics;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Shared.Popups;
using Content.Shared.Bed.Sleep;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared._Sunrise.Overlay.Systems;

namespace Content.Server._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private BlindableSystem _blindable = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;
    private static readonly string[] _mouthBlockerSlots = ["mask", "head"];
    [Dependency] private FlashImmunitySystem _flashImmunity = default!;

    private void InitializeAbilities()
    {
        SubscribeLocalEvent<VampireComponent, VampireToggleFangsActionEvent>(OnToggleFangs);

        SubscribeLocalEvent<VampireComponent, VampireGlareActionEvent>(OnGlare);

        SubscribeLocalEvent<VampireComponent, VampireSleepActionEvent>(OnSleep);
        SubscribeLocalEvent<VampireComponent, VampireSleepDoAfterEvent>(OnSleepDoAfter);

        SubscribeLocalEvent<VampireComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<VampireComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
        SubscribeLocalEvent<VampireComponent, VampireDrinkBloodDoAfterEvent>(OnDrinkDoAfter);

        SubscribeLocalEvent<VampireDevourableComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<VampireComponent, VampireDevourDoAfterEvent>(OnDevourDoAfter);

        SubscribeLocalEvent<VampireComponent, VampireRejuvenateIActionEvent>(OnRejuvenateI);
        SubscribeLocalEvent<VampireComponent, VampireRejuvenateIIActionEvent>(OnRejuvenateII);

        SubscribeLocalEvent<VampireComponent, VampireClassSelectActionEvent>(OnClassSelect);

        Subs.BuiEvents<VampireComponent>(VampireClassUiKey.Key, subs =>
        {
            subs.Event<VampireClassChosenBuiMsg>(OnVampireClassChosen);
            subs.Event<VampireClassClosedBuiMsg>(OnVampireClassClosed);
        });

    }

    private void OnUseInHand(Entity<VampireDevourableComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        if (!TryComp<VampireComponent>(user, out var vamp)
            || !TryComp<VampireBloodDrinkerComponent>(user, out var drinker))
            return;

        if (IsMouthBlocked(user))
        {
            _popup.PopupEntity(Loc.GetString("vampire-mouth-covered"), user, user);
            return;
        }

        if (drinker.MaxBloodFullness > 0f && drinker.BloodFullness >= drinker.MaxBloodFullness)
            return;

        if (!Exists(ent.Owner))
            return;

        var doAfterEv = new VampireDevourDoAfterEvent
        {
            BloodFullnessRestore = ent.Comp.BloodFullnessRestore
        };

        var dargs = new DoAfterArgs(EntityManager, user, ent.Comp.DevourDelay, doAfterEv, user, used: ent.Owner)
        {
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            BreakOnMove = false,
            BreakOnDamage = false,
            AttemptFrequency = AttemptFrequency.StartAndEnd
        };

        if (_doAfter.TryStartDoAfter(dargs))
            args.Handled = true;
    }

    private void OnDevourDoAfter(EntityUid uid, VampireComponent comp, ref VampireDevourDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Used is not { } used || !Exists(used))
            return;

        if (!TryComp<VampireBloodDrinkerComponent>(uid, out var drinker))
            return;

        if (!TryComp<VampireDevourableComponent>(used, out var devourable))
            return;

        var wasStarving = drinker.BloodFullness <= 0f;
        drinker.BloodFullness = MathF.Min(drinker.MaxBloodFullness, drinker.BloodFullness + args.BloodFullnessRestore);
        var isStarving = drinker.BloodFullness <= 0f;
        if (wasStarving != isStarving)
            _movementSpeed.RefreshMovementSpeedModifiers(uid);

        Dirty(uid, drinker);
        UpdateVampireFedAlert(uid, comp);

        _audio.PlayPvs(devourable.DevourSound, uid);
        QueueDel(used);

        args.Handled = true;
    }

    #region Helper Methods

    /// <summary>
    /// Проверяет, валидны ли координаты тайла и не заблокированы ли они
    /// </summary>
    internal bool IsValidTile(EntityCoordinates coords, EntityUid? gridUid = null, MapGridComponent? gridComp = null)
    {
        gridUid ??= _transform.GetGrid(coords);
        if (gridUid == null
            || (gridComp == null && !TryComp(gridUid.Value, out gridComp))
            || !_map.TryGetTileRef(gridUid.Value, gridComp, coords, out var tileRef))
            return false;

        return !_turf.IsSpace(tileRef) &&
            !_turf.IsTileBlocked(tileRef, CollisionGroup.Impassable) &&
            !IsTileBlockedByEntities(coords);
    }

    internal bool HasChosenClass(EntityUid uid)
        => TryComp<VampireComponent>(uid, out var vamp) && vamp.ChosenClassId != null;

    internal bool ValidateVampireClass(EntityUid uid, VampireComponent comp, ProtoId<VampireClassPrototype>? requiredClass)
    {
        _ = uid;
        if (requiredClass == null)
            return true;

        return comp.ChosenClassId == requiredClass;
    }

    /// <summary>
    /// Общая валидация вампирских способностей
    /// проверка компонента + валидация класса + стоимость действия
    /// </summary>
    internal bool CanUseVampireAbility(EntityUid uid, VampireComponent comp, EntityUid? actionEntity = null, int bloodCost = 0, bool showPopup = true)
    {
        return TryResolveVampireActionCost(uid, comp, actionEntity, bloodCost, out var resolvedCost, showPopup)
            && CanSpendBlood(uid, comp, resolvedCost, showPopup);
    }

    internal bool CheckAndConsumeGrantedVampireAction(EntityUid uid, EntityUid? actionEntity = null, int bloodCost = 0)
    {
        if (TryComp<VampireComponent>(uid, out var comp))
            return CheckAndConsumeBloodCost(uid, comp, actionEntity, bloodCost);

        return CanUseNonVampireGrantedAction(uid, actionEntity);
    }

    internal bool CheckAndConsumeBloodCost(EntityUid uid, VampireComponent comp, EntityUid? actionEntity = null, int bloodCost = 0)
    {
        if (!TryResolveVampireActionCost(uid, comp, actionEntity, bloodCost, out var resolvedCost)
            || !CanSpendBlood(uid, comp, resolvedCost))
        {
            return false;
        }

        return TrySpendBlood(uid, comp, resolvedCost);
    }

    internal bool CheckAndConsumeActionCost(EntityUid uid, VampireComponent comp, EntityUid? actionEntity)
        => CheckAndConsumeBloodCost(uid, comp, actionEntity);

    internal bool CanSpendBlood(EntityUid uid, VampireComponent comp, int bloodCost, bool showPopup = true)
    {
        if (bloodCost <= 0)
            return true;

        if (!TryComp<VampireProgressionComponent>(uid, out var progression))
            return false;

        if (progression.DrunkBlood >= bloodCost)
            return true;

        if (showPopup)
            _popup.PopupEntity(Loc.GetString("vampire-not-enough-blood"), uid, uid);

        return false;
    }

    internal bool TrySpendBlood(EntityUid uid, VampireComponent comp, int bloodCost, bool showPopup = true)
    {
        if (!CanSpendBlood(uid, comp, bloodCost, showPopup))
            return false;

        if (bloodCost <= 0)
            return true;

        if (!TryComp<VampireProgressionComponent>(uid, out var progression))
            return false;

        progression.DrunkBlood -= bloodCost;
        Dirty(uid, progression);
        UpdateVampireAlert(uid);
        return true;
    }

    internal int AddBlood(
        EntityUid uid,
        VampireComponent comp,
        float amount,
        EntityUid? target = null,
        bool countTotalBlood = true,
        bool recordTarget = true,
        bool raiseBloodDrankEvent = true)
    {
        if (amount <= 0f)
            return 0;

        if (!TryComp<VampireProgressionComponent>(uid, out var progression)
            || !TryComp<VampireBloodDrinkerComponent>(uid, out var drinker))
            return 0;

        var integerAmount = Math.Max(0, (int) amount);
        var wasStarving = drinker.BloodFullness <= 0f;

        if (integerAmount > 0)
        {
            progression.DrunkBlood += integerAmount;

            if (countTotalBlood)
                progression.TotalBlood += integerAmount;

            if (recordTarget && target is { } targetUid)
            {
                if (!drinker.BloodDrunkFromTargets.ContainsKey(targetUid))
                    drinker.BloodDrunkFromTargets[targetUid] = 0;

                drinker.BloodDrunkFromTargets[targetUid] += integerAmount;
            }
        }

        drinker.BloodFullness = MathF.Min(drinker.MaxBloodFullness, drinker.BloodFullness + amount);

        var isStarving = drinker.BloodFullness <= 0f;
        if (wasStarving != isStarving)
            _movementSpeed.RefreshMovementSpeedModifiers(uid);

        Dirty(uid, progression);
        Dirty(uid, drinker);
        UpdateVampireAlert(uid);
        UpdateVampireFedAlert(uid, comp);

        if (integerAmount > 0)
        {
            UpdateFullPower(uid, comp);
            RaiseLocalEvent(uid, new VampireProgressionChangedEvent());
        }

        if (raiseBloodDrankEvent && target is { } drankTarget)
            RaiseLocalEvent(uid, new VampireBloodDrankEvent(drankTarget, amount));

        return integerAmount;
    }

    private bool TryResolveVampireActionCost(
        EntityUid uid,
        VampireComponent comp,
        EntityUid? actionEntity,
        int bloodCost,
        out int resolvedCost,
        bool showPopup = true)
    {
        resolvedCost = Math.Max(0, bloodCost);

        if (actionEntity is not { } action)
            return true;

        if (!Exists(action))
            return false;

        if (!TryComp<VampireActionComponent>(action, out var vac))
            return true;

        if (!TryComp<VampireProgressionComponent>(uid, out var progression))
            return false;

        if (progression.TotalBlood < vac.BloodToUnlock)
            return false;

        if (!ValidateVampireClass(uid, comp, vac.RequiredClass))
            return false;

        if (vac.RequiresFullPower && !progression.FullPower)
        {
            if (showPopup)
                _popup.PopupEntity(Loc.GetString("action-vampire-not-enough-power"), uid, uid);

            return false;
        }

        if (resolvedCost <= 0 && vac.BloodCost > 0)
            resolvedCost = (int) vac.BloodCost;

        return true;
    }

    private bool CanUseNonVampireGrantedAction(EntityUid uid, EntityUid? actionEntity, bool showPopup = true)
    {
        if (actionEntity is not { } action)
            return true;

        if (!Exists(action))
            return false;

        if (!TryComp<VampireActionComponent>(action, out var vac))
            return true;

        return vac.AllowNonVampireUsers;
    }

    internal bool IsProtectedByFaith(EntityUid target)
        => HasComp<BibleUserComponent>(target);

    private bool IsInvalidDrinkTarget(EntityUid user, EntityUid target, bool showPopup = true)
    {
        if (!HasComp<VampireComponent>(target) && !HasComp<VampireThrallComponent>(target))
            return false;

        if (showPopup)
            _popup.PopupEntity(Loc.GetString("vampire-drink-invalid-target"), user, user, PopupType.MediumCaution);

        return true;
    }

    /// <summary>
    /// Проверяет, заблокирована ли позиция тайла твёрдыми сущностями (стены и т.п.)
    /// </summary>
    internal bool IsTileBlockedByEntities(EntityCoordinates coords)
    {
        // Проверка закреплённых сущностей в этой позиции, блокирующих движение
        foreach (var ent in _lookup.GetEntitiesIntersecting(_transform.ToMapCoordinates(coords), LookupFlags.Static))
        {
            // Пропускаем незакреплённые сущности
            if (!Transform(ent).Anchored)
                continue;

            // Проверка наличия физического компонента с непроходимой коллизией
            if (TryComp<PhysicsComponent>(ent, out var physics) &&
                physics.CanCollide &&
                ((physics.CollisionLayer & (int)CollisionGroup.Impassable) != 0 ||
                 (physics.CollisionMask & (int)CollisionGroup.Impassable) != 0))
                return true;

            // Проверка компонентов дверей, которые обычно блокируют движение
            if (HasComp<Shared.Doors.Components.DoorComponent>(ent))
                return true;
        }
        return false;
    }

    #endregion

    #region Base Abilities
    private void OnToggleFangs(EntityUid uid, VampireComponent comp, ref VampireToggleFangsActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<VampireBloodDrinkerComponent>(uid, out var drinker))
            return;

        drinker.FangsExtended = !drinker.FangsExtended;
        if (!drinker.FangsExtended)
            drinker.IsDrinking = false;

        _popup.PopupEntity(Loc.GetString(drinker.FangsExtended ? "vampire-fangs-extended" : "vampire-fangs-retracted"), uid, uid);

        if (comp.ActionEntities.TryGetValue(VampireComponent.ToggleFangsActionId, out var actionEntity) && _actions.GetAction(actionEntity) is { } action)
            _actions.SetToggled(action.AsNullable(), drinker.FangsExtended);
        Dirty(uid, drinker);
        args.Handled = true;
    }

    private void OnAfterInteract(EntityUid uid, VampireComponent comp, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !Exists(args.Target))
            return;

        if (TryStartDrinkFromTarget(uid, comp, args.Target.Value))
            args.Handled = true;
    }

    private void OnBeforeInteractHand(EntityUid uid, VampireComponent comp, ref BeforeInteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (Exists(args.Target) && TryStartDrinkFromTarget(uid, comp, args.Target))
            args.Handled = true;
    }

    /// <summary>
    /// Общая проверка цели питья и запуск doafter укуса (для AfterInteract и BeforeInteractHand)
    /// </summary>
    private bool TryStartDrinkFromTarget(EntityUid uid, VampireComponent comp, EntityUid target)
    {
        if (!TryComp<VampireBloodDrinkerComponent>(uid, out var drinker) || !drinker.FangsExtended)
            return false;

        if (target == uid || !HasComp<BloodstreamComponent>(target))
            return false;

        if (IsInvalidDrinkTarget(uid, target))
            return false;

        if (IsProtectedByFaith(target)
            && (!TryComp<VampireProgressionComponent>(uid, out var progression) || progression.FullPower != true))
        {
            _popup.PopupEntity(Loc.GetString("vampire-target-protected-by-faith"), uid, uid, Shared.Popups.PopupType.MediumCaution);
            return false;
        }

        if (IsMouthBlocked(uid))
        {
            _popup.PopupEntity(Loc.GetString("vampire-mouth-covered"), uid, uid);
            return false;
        }

        StartDrinkDoAfter(uid, comp, target, showPopup: true);
        return true;
    }

    /// <summary>
    /// Система проверки возможности пить из цели и обработки питья
    /// </summary>
    private void OnDrinkDoAfter(EntityUid uid, VampireComponent comp, ref VampireDrinkBloodDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<VampireBloodDrinkerComponent>(uid, out var drinker))
            return;

        if (args.Cancelled)
        {
            drinker.IsDrinking = false;
            return;
        }

        if (!drinker.FangsExtended
            || args.Args.Target == null
            || !HasComp<BloodstreamComponent>(args.Args.Target.Value)
            )
        {
            drinker.IsDrinking = false;
            return;
        }

        var target = args.Args.Target.Value;

        if (IsInvalidDrinkTarget(uid, target, showPopup: false))
        {
            drinker.IsDrinking = false;
            return;
        }

        if (!drinker.BloodDrunkFromTargets.TryGetValue(target, out var drunkFromTarget))
            drunkFromTarget = 0;

        if (drunkFromTarget >= drinker.MaxBloodPerTarget)
        {
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-maxed", ("amount", drinker.MaxBloodPerTarget)), uid, uid, Shared.Popups.PopupType.MediumCaution);
            drinker.IsDrinking = false;
            return;
        }


        if (!TryComp<MobStateComponent>(target, out var mobState)) //Является ли сущность мобом вообще?
        {
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-not-viable"), uid, uid, Shared.Popups.PopupType.MediumCaution);
            drinker.IsDrinking = false;
            return;
        }

        var sipAmount = drinker.SipAmount;
        var sipInefficiency = GetTargetBloodEfficiency(target, mobState.CurrentState);

        if (sipInefficiency <= 0f) //Если эффективность равна 0, продолжать нет смысла
        {
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-rot"), uid, uid, Shared.Popups.PopupType.MediumCaution);
            drinker.IsDrinking = false;
            return;
        }

        sipInefficiency = 1f / sipInefficiency;

        var maxCanDrink = drinker.MaxBloodPerTarget - drunkFromTarget;
        var actualSipAmount = MathF.Min(sipAmount, maxCanDrink);
        if (!TryComp<BloodstreamComponent>(target, out var blood)) //Есть ли у цели кровеносная система?
        {
            drinker.IsDrinking = false; //Не удалось снизить уровень крови
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-empty"), uid, uid, Shared.Popups.PopupType.MediumCaution);
            return;
        }

        //пытаемся вытянуть уровень крови цели
        var targetBloodLevel = _blood.GetBloodLevel(target) * blood.BloodReferenceSolution.MaxVolume.Value / 100; //получаем текущий объём крови цели в единицах
        if (targetBloodLevel <= 0.0f) //Проверка, есть ли у цели вообще кровь для питья
        {
            drinker.IsDrinking = false; //Не удалось снизить уровень крови
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-empty"), uid, uid, Shared.Popups.PopupType.MediumCaution);
            return;
        }
        else if (targetBloodLevel <= actualSipAmount * sipInefficiency) //Проверка, не пытаемся ли выпить слишком много крови; если так — уменьшаем выпиваемый объём
            actualSipAmount = targetBloodLevel / sipInefficiency;

        // Вытягиваем дополнительную кровь цели с учётом неэффективности глотка
        if (_blood.TryModifyBloodLevel(target, -actualSipAmount * sipInefficiency))
        {
            var targetIsHumanoid = HasComp<HumanoidProfileComponent>(target);
            AddBlood(uid, comp, actualSipAmount, target, countTotalBlood: targetIsHumanoid);

            //Урон от укуса
            //Немного дополнительного урона, чтобы отпугнуть от сдачи крови
            var biteDamage = new DamageSpecifier();
            biteDamage += new DamageSpecifier(_proto.Index<DamageTypePrototype>(_pierceTypeId), drinker.SipPierceDamage * actualSipAmount); //5 колющего урона за 10 единиц
            _damageableSystem.TryChangeDamage(target, biteDamage, ignoreResistances: true);
            _blood.TryModifyBleedAmount(target, 1);

            //Добавляем слепоту вместо болезни
            if (TryComp<BlindableComponent>(target, out var blindable)
                && TryComp<VampireProgressionComponent>(uid, out var progression)
                && 2 <= progression.BlindInc)
            {
                _blindable.AdjustEyeDamage((target, blindable), 1);
                progression.BlindInc = 0;
            }
            else if (TryComp<VampireProgressionComponent>(uid, out var progression2) && progression2.BlindInc < 2)
                progression2.BlindInc += 1;

            // Базовое лечение
            var baseHealSpec = new DamageSpecifier();
            if (TryComp<VampireHealingComponent>(uid, out var healing))
            {
                baseHealSpec += new DamageSpecifier(_proto.Index<DamageGroupPrototype>(_bruteGroupId), -healing.VampHealBrute);
                baseHealSpec += new DamageSpecifier(_proto.Index<DamageGroupPrototype>(_burnGroupId), -healing.VampHealBurn);
                baseHealSpec += new DamageSpecifier(_proto.Index<DamageTypePrototype>(_poisonTypeId), -healing.VampHealPois);
                baseHealSpec += new DamageSpecifier(_proto.Index<DamageTypePrototype>(_oxyLossTypeId), -healing.VampHealAsphyxiation);
                _damageableSystem.TryChangeDamage(uid, baseHealSpec, true);
            }

            _audio.PlayPvs(drinker.BiteSound, target, AudioParams.Default.WithVolume(-7f));
            var targetCoords = Transform(target).Coordinates;
            Spawn("WeaponArcBite", targetCoords);

            var currentDrunkFromTarget = drinker.BloodDrunkFromTargets.GetValueOrDefault(target, 0);
            if (drinker.FangsExtended && currentDrunkFromTarget < drinker.MaxBloodPerTarget)
            {
                drinker.IsDrinking = false;
                StartDrinkDoAfter(uid, comp, target, showPopup: false);
            }
            else
            {
                drinker.IsDrinking = false;
                if (currentDrunkFromTarget >= drinker.MaxBloodPerTarget)
                    _popup.PopupEntity(Loc.GetString("vampire-drink-target-hard-max", ("amount", drinker.MaxBloodPerTarget)), uid, uid);
            }
        }
        else
        {
            drinker.IsDrinking = false; //Не удалось снизить уровень крови
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-empty"), uid, uid, Shared.Popups.PopupType.MediumCaution);
            return;
        }


    }

    /// <summary>
    /// Определяет эффективность крови цели через BloodSourceComponent на цели.
    /// Если компонента нет — берёт стандартные значения для живой/мёртвой цели.
    /// </summary>
    private float GetTargetBloodEfficiency(EntityUid target, MobState mobState)
    {
        if (TryComp<BloodSourceComponent>(target, out var bloodSource))
        {
            var efficiency = bloodSource.BaseEfficiency;

            if (mobState == Shared.Mobs.MobState.Dead)
            {
                efficiency *= bloodSource.DeadEfficiency;

                if (TryComp<PerishableComponent>(target, out var rot))
                    efficiency *= bloodSource.GetRotEfficiency(rot.RotAccumulator);
            }

            return efficiency;
        }

        // Fallback для целей без BloodSourceComponent: нейтральная эффективность,
        // без жёстких рамок «человек/животное»
        var fallbackEfficiency = 1.0f;
        if (mobState == Shared.Mobs.MobState.Dead)
        {
            fallbackEfficiency *= 0.75f;
            if (TryComp<PerishableComponent>(target, out var rot))
                fallbackEfficiency *= rot.RotAccumulator.TotalSeconds switch
                {
                    < 30 => 1.0f,
                    < 210 => 0.5f,
                    < 405 => 0.25f,
                    < 600 => 0.1f,
                    _ => 0.0f,
                };
        }

        return fallbackEfficiency;
    }

    partial void UpdateVampireAlert(EntityUid uid)
        => _alerts.ShowAlert(uid, "VampireBlood");

    partial void UpdateVampireFedAlert(EntityUid uid, VampireComponent? comp)
    {
        if (!Resolve(uid, ref comp, false)
            || !TryComp<VampireBloodDrinkerComponent>(uid, out var drinker))
            return;

        var frac = drinker.MaxBloodFullness <= 0f ? 0f : drinker.BloodFullness / drinker.MaxBloodFullness;
        var sev = (short)Math.Clamp((int)MathF.Ceiling(frac * 4f) + 1, 1, 5);
        _alerts.ShowAlert(uid, "VampireFed", sev);
    }

    private void StartDrinkDoAfter(EntityUid uid, VampireComponent comp, EntityUid target, bool showPopup)
    {
        if (!TryComp<VampireBloodDrinkerComponent>(uid, out var drinker))
            return;

        if (drinker.IsDrinking)
            return;

        if (IsMouthBlocked(uid))
        {
            if (showPopup)
                _popup.PopupEntity(Loc.GetString("vampire-mouth-covered"), uid, uid);
            return;
        }

        var dargs = new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(1.25), new VampireDrinkBloodDoAfterEvent(), uid, target)
        {
            DistanceThreshold = drinker.BiteDistanceThreshold,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
            AttemptFrequency = AttemptFrequency.StartAndEnd
        };

        if (_doAfter.TryStartDoAfter(dargs))
        {
            drinker.IsDrinking = true;
            if (showPopup)
                _popup.PopupEntity(Loc.GetString("vampire-drink-start", ("target", Identity.Entity(target, EntityManager))), uid, uid);
        }
    }

    /// <summary>
	///     При использовании действия усыпления цели: проверяет, можно ли усыпить цель и хватает ли крови, затем запускает doafter
	/// </summary>
    private void OnSleep(EntityUid uid, VampireComponent comp, ref VampireSleepActionEvent args)
    {
        if (args.Handled || !Exists(args.Target))
            return;


        var actionEntity = args.Action.Owner;

        if (!TryGetActionBloodCost(actionEntity, out var bloodCost))
            return;

        var target = args.Target;

        if (target == uid)
            return;

        if (IsProtectedByFaith(target)
            && (!TryComp<VampireProgressionComponent>(uid, out var progression) || progression.FullPower != true))
        {
            _popup.PopupEntity(Loc.GetString("vampire-target-protected-by-faith"), uid, uid, Shared.Popups.PopupType.MediumCaution);
            return;
        }

        if (_flashImmunity.HasFlashImmunityVisionBlockers(target))
        {
            _popup.PopupEntity(Loc.GetString("vampire-sleep-protected"), uid, uid, PopupType.MediumCaution);
            return;
        }

        if (!TryComp<VampireProgressionComponent>(uid, out var sleepProgression))
            return;

        if (sleepProgression.DrunkBlood < bloodCost)
        {
            _popup.PopupEntity(Loc.GetString("vampire-not-enough-blood"), uid, uid, PopupType.MediumCaution);
            return;
        }
        var doAfter = new DoAfterArgs(EntityManager, uid, args.ChannelTime, new VampireSleepDoAfterEvent { BloodCost = bloodCost }, uid, target)
        {
            DistanceThreshold = args.SleepDistanceThreshold,
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
            MovementThreshold = args.SleepMovementThreshold,
            RequireCanInteract = true,
            BlockDuplicate = true,
            CancelDuplicate = true
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        args.Handled = true;
    }

    private bool TryGetActionBloodCost(EntityUid actionEntity, out int bloodCost)
    {
        bloodCost = 0;

        if (!Exists(actionEntity) || !TryComp<VampireActionComponent>(actionEntity, out var actionComp))
            return false;

        bloodCost = (int)Math.Max(actionComp.BloodCost, 0);
        return true;
    }

    /// <summary>
	///     Вызывается после завершения doafter усыпления: повторно проверяет, не получила ли цель иммунитет, и если нет — списывает кровь и усыпляет.
	/// </summary>
    private void OnSleepDoAfter(EntityUid uid, VampireComponent comp, ref VampireSleepDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target == null)
            return;

        var target = args.Target.Value;

        if (_flashImmunity.HasFlashImmunityVisionBlockers(target))
        {
            _popup.PopupEntity(Loc.GetString("vampire-sleep-protected"), uid, uid, PopupType.MediumCaution);
            return;
        }

        if (HasComp<MindShieldComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("vampire-sleep-shielded"), uid, uid, PopupType.SmallCaution);
            return;
        }

        if (!CheckAndConsumeBloodCost(uid, comp, null, args.BloodCost))
            return;

        //Усыпляем цель
        _statusEffects.TryAddStatusEffectDuration(target, SleepingSystem.StatusEffectForcedSleeping, args.Duration);
        args.Handled = true;
    }

    /// <summary>
    /// Действие, оглушающее мобов рядом на короткое время
    /// </summary>
    private void OnGlare(EntityUid uid, VampireComponent comp, ref VampireGlareActionEvent args)
    {
        //Если вампир не видит, он не может применить взгляд
        if (TryComp<BlindableComponent>(uid, out var blindable) && blindable.IsBlind)
            return;

        if (args.Handled
            || !comp.ActionEntities.TryGetValue(VampireComponent.GlareActionId, out var actionEntity)
            || !CheckAndConsumeBloodCost(uid, comp, actionEntity))
            return;

        if (!TryComp<VampireProgressionComponent>(uid, out var progression))
            return;

        // Находим цели в радиусе 1 клетки вокруг вампира
        var targets = _lookup.GetEntitiesInRange(uid, args.Range, LookupFlags.Dynamic | LookupFlags.Sundries);

        var ourXform = Transform(uid);
        var ourDirection = ourXform.LocalRotation.ToWorldVec();
        var ourPosition = ourXform.LocalPosition;
        var effectScale = 1.0f;

        foreach (var target in targets)
        {
            if (target == uid)
                continue;

            //сбрасываем effectScale для следующей возможной цели
            effectScale = 1.0f;

            if (_flashImmunity.HasFlashImmunityVisionBlockers(target))
            {
                if (progression.TotalBlood < progression.MidPowerThreshold)
                    effectScale = args.FlashImmunityEffectScaleWeak; //ниже среднего
                else if (progression.TotalBlood < progression.HighPowerThreshold)
                    effectScale = args.FlashImmunityEffectScaleMid; //средний - высокий
                else if (progression.TotalBlood < progression.FullPowerThreshold)
                    effectScale = args.FlashImmunityEffectScaleStrong; //высокий - полный
            }

            if (progression.FullPower) //Если вампир на полной силе, эффект немного усиливается независимо от защиты от вспышек
                effectScale = args.GlareEffectScaleFull;

            if (effectScale <= 0) //Если эффект обнулён, продолжать нет смысла.
                continue;

            var targetPosition = Transform(target).LocalPosition;
            var rawVector = targetPosition - ourPosition;
            if (rawVector == Vector2.Zero)
                continue;

            var vectorToTarget = Vector2.Normalize(rawVector);

            var dot = Vector2.Dot(ourDirection, vectorToTarget);

            if (!TryComp<StaminaComponent>(target, out var stam))
                continue;

            var knockedDown = HasComp<KnockedDownComponent>(target);

            // Если цель спереди
            if (dot > args.DotForwardLimit && !knockedDown)
            {
                _stun.TryAddParalyzeDuration(target, args.FrontParalyzeDuration * effectScale);

                _stamina.TakeStaminaDamage(target, args.FrontStaminaDamage * effectScale, stam, source: uid);

                // Заглушаем цель
                TryInjectReagents(target, args.Reagents, effectScale);

                StartGlareDotEffect(target, uid, args.DotStaminaDamage * effectScale, args.DotTickCount, args.DotTickInterval);
            }
            // Если цель сзади
            else if (dot < args.DotBackwardLimit && !knockedDown)
                _stamina.TakeStaminaDamage(target, args.BehindStaminaDamage * effectScale, stam, source: uid);
            // иначе цель сбоку
            else
            {
                _stun.TryAddParalyzeDuration(target, args.SideParalyzeDuration * effectScale);

                _stamina.TakeStaminaDamage(target, args.SideStaminaDamage * effectScale, stam, source: uid);
            }
        }

        args.Handled = true;
    }

    /// <summary>
    /// Пытается ввести указанный химикат
    /// </summary>
    private bool TryInjectReagents(EntityUid target, Dictionary<string, FixedPoint2> reagents, float effectScale)
    {
        var solution = new Solution();
        foreach (var reagent in reagents)
            solution.AddReagent(reagent.Key, reagent.Value * effectScale);
        if (!_solution.TryGetInjectableSolution(target, out var targetSolution, out var _))
            return false;

        if (!_solution.TryAddSolution(targetSolution.Value, solution))
            return false;

        return true;
    }

    private void StartGlareDotEffect(EntityUid target, EntityUid source, float damage, int tickCount, TimeSpan tickInterval)
    {
        if (tickCount <= 0 || !Exists(target) || !Exists(source))
            return;

        var active = EnsureComp<ActiveVampireGlareDotComponent>(target);
        active.Source = source;
        active.StaminaDamage = damage;
        active.TicksRemaining = tickCount;
        active.TickInterval = tickInterval;
        active.NextTick = _timing.CurTime;
    }

    private void OnRejuvenateI(EntityUid uid, VampireComponent comp, ref VampireRejuvenateIActionEvent args)
    {
        if (args.Handled
            || !comp.ActionEntities.TryGetValue(VampireComponent.RejuvenateIActionId, out var actionEntity)
            || !CheckAndConsumeBloodCost(uid, comp, actionEntity))
            return;

        ResetRejuvenateEffects(uid, args.ResetStamina, args.RemoveStuns);

        args.Handled = true;
    }

    private void OnRejuvenateII(EntityUid uid, VampireComponent comp, ref VampireRejuvenateIIActionEvent args)
    {
        if (args.Handled
            || !comp.ActionEntities.TryGetValue(VampireComponent.RejuvenateIIActionId, out var actionEntity)
            || !CheckAndConsumeBloodCost(uid, comp, actionEntity))
            return;

        ResetRejuvenateEffects(uid, args.ResetStamina, args.RemoveStuns);
        PurgeRejuvenateReagents(uid, args);
        StartRejuvenateHealing(uid, args);

        args.Handled = true;
    }

    private void ResetRejuvenateEffects(EntityUid uid, bool resetStamina, bool removeStuns)
    {
        if (resetStamina && TryComp<StaminaComponent>(uid, out var stamina))
        {
            stamina.StaminaDamage = 0f;
            _stamina.ExitStamCrit(uid, stamina);
            _stamina.AdjustStatus((uid, stamina));
            RemComp<ActiveStaminaComponent>(uid);
            _statusEffects.TryRemoveStatusEffect(uid, SharedStaminaSystem.StaminaLow);
            _stamina.UpdateStaminaVisuals((uid, stamina));
            Dirty(uid, stamina);
        }

        if (!removeStuns)
            return;

        _statusEffects.TryRemoveStatusEffect(uid, SharedStunSystem.StunId);
        _stun.TryUnstun(uid);
        RemComp<KnockedDownComponent>(uid);
    }

    private void PurgeRejuvenateReagents(EntityUid uid, VampireRejuvenateIIActionEvent args)
    {
        if (args.ReagentPurgeAmount <= FixedPoint2.Zero
            || !TryComp<BloodstreamComponent>(uid, out var blood)
            || !_solution.ResolveSolution(uid, blood.BloodSolutionName, ref blood.BloodSolution, out var bloodstreamSolution))
        {
            return;
        }

        var solEnt = blood.BloodSolution.Value;
        var toRemove = FixedPoint2.Zero;

        foreach (var quant in bloodstreamSolution.Contents.ToArray())
        {
            if (toRemove >= args.ReagentPurgeAmount)
                break;

            if (!_proto.TryIndex<ReagentPrototype>(quant.Reagent.Prototype, out var proto)
                || proto.Metabolisms == null
                || !proto.Metabolisms.Metabolisms.Keys.Any(args.PurgedMetabolismStages.Contains))
                continue;

            var remaining = args.ReagentPurgeAmount - toRemove;
            var removeAmt = FixedPoint2.Min(quant.Quantity, remaining);

            _solution.RemoveReagent(solEnt, quant.Reagent, removeAmt);
            toRemove += removeAmt;
        }
    }

    private void StartRejuvenateHealing(EntityUid uid, VampireRejuvenateIIActionEvent args)
    {
        if (args.HealTicks <= 0)
            return;

        var active = EnsureComp<ActiveVampireRejuvenateComponent>(uid);
        active.TicksRemaining = args.HealTicks;
        active.TickInterval = args.HealTickInterval;
        active.NextTick = _timing.CurTime;
        active.HealGroups = new Dictionary<string, FixedPoint2>(args.HealGroups);
        active.HealTypes = new Dictionary<string, FixedPoint2>(args.HealTypes);
    }

    private void ProcessActiveVampireEffects(TimeSpan now)
    {
        var rejuvenateQuery = EntityQueryEnumerator<ActiveVampireRejuvenateComponent>();
        while (rejuvenateQuery.MoveNext(out var uid, out var rejuvenate))
        {
            if (now < rejuvenate.NextTick)
                continue;

            ApplyConfiguredHeal(uid, rejuvenate.HealGroups, rejuvenate.HealTypes);
            rejuvenate.TicksRemaining--;

            if (rejuvenate.TicksRemaining <= 0)
            {
                RemComp<ActiveVampireRejuvenateComponent>(uid);
                continue;
            }

            rejuvenate.NextTick = now + rejuvenate.TickInterval;
        }

        var glareQuery = EntityQueryEnumerator<ActiveVampireGlareDotComponent>();
        while (glareQuery.MoveNext(out var uid, out var glare))
        {
            if (now < glare.NextTick)
                continue;

            if (!Exists(glare.Source))
            {
                RemComp<ActiveVampireGlareDotComponent>(uid);
                continue;
            }

            if (TryComp<StaminaComponent>(uid, out var stam) && !stam.Critical)
                _stamina.TakeStaminaDamage(uid, glare.StaminaDamage, stam, source: glare.Source);

            glare.TicksRemaining--;
            if (glare.TicksRemaining <= 0)
            {
                RemComp<ActiveVampireGlareDotComponent>(uid);
                continue;
            }

            glare.NextTick = now + glare.TickInterval;
        }

        var pacifyQuery = EntityQueryEnumerator<ActiveVampirePacifyComponent>();
        while (pacifyQuery.MoveNext(out var uid, out var pacify))
        {
            if (now < pacify.EndTime)
                continue;

            RemComp<ActiveVampirePacifyComponent>(uid);
            RemComp<PacifiedComponent>(uid);
        }

        var invisibleQuery = EntityQueryEnumerator<ActiveVampireInvisibilityComponent>();
        while (invisibleQuery.MoveNext(out var uid, out var invis))
        {
            if (now < invis.EndTime)
                continue;

            RemComp<ActiveVampireInvisibilityComponent>(uid);
            RestoreVampireInvisibilityStealth(uid, invis);
        }
    }

    private void RestoreVampireInvisibilityStealth(EntityUid uid, ActiveVampireInvisibilityComponent invis)
    {
        if (!TryComp<StealthComponent>(uid, out var stealth))
            return;

        if (!invis.HadStealthComponent)
        {
            RemComp<StealthComponent>(uid);
            return;
        }

        _stealth.SetEnabled(uid, invis.PreviousStealthEnabled, stealth);
        _stealth.SetVisibility(uid, invis.PreviousStealthVisibility, stealth);
    }

    private void ApplyConfiguredHeal(
        EntityUid uid,
        IReadOnlyDictionary<string, FixedPoint2> healGroups,
        IReadOnlyDictionary<string, FixedPoint2> healTypes)
    {
        var healSpec = new DamageSpecifier();

        foreach (var (groupId, amount) in healGroups)
        {
            if (amount <= FixedPoint2.Zero || !_proto.TryIndex<DamageGroupPrototype>(groupId, out var group))
                continue;

            healSpec += new DamageSpecifier(group, -amount);
        }

        foreach (var (typeId, amount) in healTypes)
        {
            if (amount <= FixedPoint2.Zero || !_proto.TryIndex<DamageTypePrototype>(typeId, out var type))
                continue;

            healSpec += new DamageSpecifier(type, -amount);
        }

        if (healSpec.Empty)
            return;

        _damageableSystem.TryChangeDamage(uid, healSpec, true);
    }

    private void OnClassSelect(EntityUid uid, VampireComponent comp, ref VampireClassSelectActionEvent args)
    {
        if (args.Handled)
            return;

        if (HasChosenClass(uid))
        {
            args.Handled = true;
            return;
        }

        OpenClassUi(uid, comp);
        args.Handled = true;
        Dirty(uid, comp);
    }

    #endregion

    #region Full Power, Passives
    /// <summary>
    /// Проверка уровня полной силы вампира
    /// </summary>
    private void UpdateFullPower(EntityUid uid, VampireComponent comp)
    {
        if (!TryComp<VampireProgressionComponent>(uid, out var progression)
            || !TryComp<VampireBloodDrinkerComponent>(uid, out var drinker))
            return;

        int uniqueHumanoids = 0;
        foreach (var kv in drinker.BloodDrunkFromTargets.Keys)
            if (Exists(kv) && HasComp<HumanoidProfileComponent>(kv))
                uniqueHumanoids++;
        progression.UniqueHumanoidVictims = uniqueHumanoids;
        var prev = progression.FullPower;
        progression.FullPower = progression.TotalBlood >= progression.FullPowerThreshold && uniqueHumanoids >= progression.FullPowerUniqueHumanoids;
        if (!prev && progression.FullPower)
        {
            _popup.PopupEntity(Loc.GetString("vampire-full-power-achieved"), uid, uid);
            var ev = new VampireFullPowerAchievedEvent();
            RaiseLocalEvent(uid, ev);
        }
        Dirty(uid, progression);
    }

    private bool IsMouthBlocked(EntityUid uid)
    {
        if (!HasComp<InventoryComponent>(uid))
            return false;

        var slots = _mouthBlockerSlots;
        foreach (var slot in slots)
            if (_inventory.TryGetSlotEntity(uid, slot, out var ent) &&
                TryComp<IngestionBlockerComponent>(ent.Value, out var blocker) &&
                blocker.Enabled)

                return true;

        return false;
    }

    #endregion
}
