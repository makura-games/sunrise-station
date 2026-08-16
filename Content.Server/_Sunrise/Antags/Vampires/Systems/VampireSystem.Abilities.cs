using Content.Server.Bible.Components;
using Content.Shared._Sunrise.Antags.Vampires;
using Content.Shared._Sunrise.Antags.Vampires.Events;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Components.Effects;
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
using Content.Shared.Mobs.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
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
using Robust.Shared.Random;
using System.Linq;
using System.Numerics;
using Content.Shared.Popups;
using Content.Shared.Bed.Sleep;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Flash;


namespace Content.Server._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly BlindableSystem _blindable = default!;
    private static readonly SoundSpecifier BiteSound = new SoundPathSpecifier("/Audio/Effects/bite.ogg");
    private static readonly SoundSpecifier DevourSound = new SoundPathSpecifier("/Audio/Effects/demon_consume.ogg");

    // Антимета
    private static readonly LocId[] SleepTargetPopupIds =
    [
        "vampire-sleep-target-warning-1",
        "vampire-sleep-target-warning-2",
        "vampire-sleep-target-warning-3",
        "vampire-sleep-target-warning-4",
        "vampire-sleep-target-warning-5",
    ];

    private void InitializeAbilities()
    {
        SubscribeLocalEvent<VampireComponent, VampireToggleFangsActionEvent>(OnToggleFangs);

        SubscribeLocalEvent<VampireComponent, VampireGlareActionEvent>(OnGlare);

        SubscribeLocalEvent<VampireComponent, VampireSleepActionEvent>(OnSleep);
        SubscribeLocalEvent<VampireComponent, DoAfterAttemptEvent<VampireSleepDoAfterEvent>>(OnSleepDoAfterAttempt);
        SubscribeLocalEvent<VampireComponent, VampireSleepDoAfterEvent>(OnSleepDoAfter);

        SubscribeLocalEvent<VampireComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<VampireComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
        SubscribeLocalEvent<VampireComponent, VampireDrinkBloodDoAfterEvent>(OnDrinkDoAfter);

        SubscribeLocalEvent<VampireDevourableComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<VampireComponent, VampireDevourDoAfterEvent>(OnDevourDoAfter);

        SubscribeLocalEvent<VampireComponent, VampireRejuvenateIActionEvent>(OnRejuvenateI);
        SubscribeLocalEvent<VampireComponent, VampireRejuvenateIIActionEvent>(OnRejuvenateII);

    }

    private void OnUseInHand(Entity<VampireDevourableComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        if (!TryComp<VampireComponent>(user, out var vamp))
            return;

        if (IsMouthBlocked(user))
        {
            _popup.PopupEntity(Loc.GetString("vampire-mouth-covered"), user, user);
            return;
        }

        if (vamp.MaxBloodFullness > 0f && vamp.BloodFullness >= vamp.MaxBloodFullness)
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

    private void OnDevourDoAfter(Entity<VampireComponent> ent, ref VampireDevourDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (args.Cancelled)
            return;

        if (args.Used is not { } used || !Exists(used))
            return;

        var wasStarving = ent.Comp.BloodFullness <= 0f;
        ent.Comp.BloodFullness = MathF.Min(ent.Comp.MaxBloodFullness, ent.Comp.BloodFullness + args.BloodFullnessRestore);
        var isStarving = ent.Comp.BloodFullness <= 0f;
        if (wasStarving != isStarving)
            _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);

        Dirty(ent);
        UpdateVampireFedAlert(ent);

        _audio.PlayPvs(DevourSound, ent);
        QueueDel(used);

        args.Handled = true;
    }

    #region Helper Methods

    /// <summary>
    /// Check if tile coordinates are valid and not blocked
    /// </summary>
    internal bool IsValidTile(EntityCoordinates coords, EntityUid? gridUid = null, MapGridComponent? gridComp = null)
    {
        gridUid ??= _transform.GetGrid(coords);
        if (gridUid is null)
            return false;

        if (gridComp is null && !TryComp(gridUid.Value, out gridComp))
            return false;

        if (!_map.TryGetTileRef(gridUid.Value, gridComp, coords, out var tileRef))
            return false;

        return !_turf.IsSpace(tileRef) &&
            !_turf.IsTileBlocked(tileRef, CollisionGroup.Impassable) &&
            !IsTileBlockedByEntities(coords);
    }

    internal bool CheckAndConsumeBloodCost(Entity<VampireComponent> ent, EntityUid? actionEntity = null, int bloodCost = 0)
    {
        if (!TryResolveVampireActionCost(ent, actionEntity, bloodCost, out var resolvedCost))
            return false;

        if (!CanSpendBlood(ent, resolvedCost))
            return false;

        return TrySpendBlood(ent, resolvedCost);
    }

    internal bool CanSpendBlood(Entity<VampireComponent> ent, int bloodCost, bool showPopup = true)
    {
        if (bloodCost <= 0)
            return true;

        if (ent.Comp.DrunkBlood >= bloodCost)
            return true;

        if (showPopup)
            _popup.PopupEntity(Loc.GetString("vampire-not-enough-blood"), ent.Owner, ent.Owner);

        return false;
    }

    internal bool TrySpendBlood(Entity<VampireComponent> ent, int bloodCost, bool showPopup = true)
    {
        if (!CanSpendBlood(ent, bloodCost, showPopup))
            return false;

        if (bloodCost <= 0)
            return true;

        ent.Comp.DrunkBlood -= bloodCost;
        Dirty(ent);
        UpdateVampireAlert(ent.Owner);
        return true;
    }

    internal int AddBlood(
        Entity<VampireComponent> ent,
        float amount,
        EntityUid? target = null,
        bool countTotalBlood = true,
        bool recordTarget = true)
    {
        if (amount <= 0f)
            return 0;

        var storedAmount = amount + ent.Comp.DrunkBloodRemainder;
        var integerAmount = Math.Max(0, (int)storedAmount);
        ent.Comp.DrunkBloodRemainder = storedAmount - integerAmount;
        var wasStarving = ent.Comp.BloodFullness <= 0f;

        if (integerAmount > 0)
            ent.Comp.DrunkBlood += integerAmount;

        var totalBloodAdded = 0;
        if (countTotalBlood)
        {
            var totalAmount = amount + ent.Comp.TotalBloodRemainder;
            totalBloodAdded = Math.Max(0, (int)totalAmount);
            ent.Comp.TotalBloodRemainder = totalAmount - totalBloodAdded;
            ent.Comp.TotalBlood += totalBloodAdded;
        }

        if (recordTarget && target is { } targetUid)
        {
            if (!ent.Comp.BloodDrunkFromTargets.ContainsKey(targetUid))
                ent.Comp.BloodDrunkFromTargets[targetUid] = 0f;

            ent.Comp.BloodDrunkFromTargets[targetUid] += amount;
        }

        ent.Comp.BloodFullness = MathF.Min(ent.Comp.MaxBloodFullness, ent.Comp.BloodFullness + amount);

        var isStarving = ent.Comp.BloodFullness <= 0f;
        if (wasStarving != isStarving)
            _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);

        Dirty(ent);
        UpdateVampireAlert(ent.Owner);
        UpdateVampireFedAlert(ent);

        if (totalBloodAdded > 0)
            UpdatePowerLevel(ent);

        return integerAmount;
    }

    private bool TryResolveVampireActionCost(
        Entity<VampireComponent> ent,
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

        if (ent.Comp.PowerLevel < vac.RequiredPowerLevel)
        {
            if (showPopup)
                _popup.PopupEntity(Loc.GetString("action-vampire-not-enough-power"), ent.Owner, ent.Owner);

            return false;
        }

        if (resolvedCost <= 0 && vac.BloodCost > 0)
            resolvedCost = (int)vac.BloodCost;

        return true;
    }

    internal bool IsProtectedByFaith(EntityUid target)
        => HasComp<BibleUserComponent>(target);

    private bool IsInvalidDrinkTarget(EntityUid user, EntityUid target, bool showPopup = true)
    {
        if (!HasComp<VampireComponent>(target))
            return false;

        if (showPopup)
            _popup.PopupEntity(Loc.GetString("vampire-drink-invalid-target"), user, user, PopupType.MediumCaution);

        return true;
    }

    /// <summary>
    /// Checks if a tile position is blocked by solid entities(walls etc.)
    /// </summary>
    internal bool IsTileBlockedByEntities(EntityCoordinates coords)
    {
        // Check for anchored entities in this position that block movement
        foreach (var ent in _lookup.GetEntitiesIntersecting(_transform.ToMapCoordinates(coords), LookupFlags.Static))
        {
            // Skip non anchored entities
            if (!Transform(ent).Anchored)
                continue;

            // Check if entity has a physics component with impassable collision
            if (TryComp<PhysicsComponent>(ent, out var physics) &&
                physics.CanCollide &&
                ((physics.CollisionLayer & (int)CollisionGroup.Impassable) != 0 ||
                 (physics.CollisionMask & (int)CollisionGroup.Impassable) != 0))
                return true;

            // Check for door components that typically block movement
            if (HasComp<Shared.Doors.Components.DoorComponent>(ent))
                return true;
        }
        return false;
    }

    #endregion

    #region Base Abilities
    private void OnToggleFangs(Entity<VampireComponent> ent, ref VampireToggleFangsActionEvent args)
    {
        if (args.Handled)
            return;

        ent.Comp.FangsExtended = !ent.Comp.FangsExtended;
        if (!ent.Comp.FangsExtended)
            ent.Comp.IsDrinking = false;

        if (ent.Comp.ActionEntities.TryGetValue(VampireFangsActionId, out var actionEntity) &&
            _actions.GetAction(actionEntity) is { } action)
            _actions.SetToggled(action.AsNullable(), ent.Comp.FangsExtended);

        Dirty(ent);
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

    private void OnBeforeInteractHand(Entity<VampireComponent> ent, ref BeforeInteractHandEvent args)
    {
        if (args.Handled || !ent.Comp.FangsExtended)
            return;

        var target = args.Target;
        if (!Exists(target) || target == ent.Owner)
            return;

        var hasBloodstream = HasComp<BloodstreamComponent>(target);
        if (!hasBloodstream && !HasComp<InteractionPopupComponent>(target))
            return;

        args.Handled = true;

        if (!hasBloodstream)
            return;

        TryStartDrinkBlood(ent, target);
    }

    private bool TryStartDrinkBlood(Entity<VampireComponent> ent, EntityUid target)
    {
        if (IsInvalidDrinkTarget(ent.Owner, target))
            return false;

        if (IsProtectedByFaith(target) && ent.Comp.PowerLevel < VampirePowerLevel.Ancient)
        {
            _popup.PopupEntity(
                Loc.GetString("vampire-target-protected-by-faith"),
                ent.Owner,
                ent.Owner,
                PopupType.MediumCaution);
            return false;
        }

        if (IsMouthBlocked(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("vampire-mouth-covered"), ent.Owner, ent.Owner);
            return false;
        }

        StartDrinkDoAfter(ent, target, showPopup: true);
        return true;
    }

    /// <summary>
    /// System for checking if a target can be drank from and handling the drinking
    /// </summary>
    private void OnDrinkDoAfter(Entity<VampireComponent> ent, ref VampireDrinkBloodDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (args.Cancelled)
        {
            ent.Comp.IsDrinking = false;
            return;
        }

        if (!ent.Comp.FangsExtended)
        {
            ent.Comp.IsDrinking = false;
            return;
        }

        if (args.Args.Target is not { } targetUid)
        {
            ent.Comp.IsDrinking = false;
            return;
        }

        if (!HasComp<BloodstreamComponent>(targetUid))
        {
            ent.Comp.IsDrinking = false;
            return;
        }

        if (IsInvalidDrinkTarget(ent.Owner, targetUid, showPopup: false))
        {
            ent.Comp.IsDrinking = false;
            return;
        }

        if (!ent.Comp.BloodDrunkFromTargets.TryGetValue(targetUid, out var drunkFromTarget))
            drunkFromTarget = 0;

        if (drunkFromTarget >= ent.Comp.MaxBloodPerTarget)
        {
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-maxed", ("amount", ent.Comp.MaxBloodPerTarget)), ent.Owner, ent.Owner, Shared.Popups.PopupType.MediumCaution);
            ent.Comp.IsDrinking = false;
            return;
        }

        var targetIsHumanoid = HasComp<HumanoidAppearanceComponent>(targetUid);
        var bloodEfficiency = targetIsHumanoid ? 1f : ent.Comp.AnimalEfficiency;

        if (TryComp<MobStateComponent>(targetUid, out var mobState) &&
            mobState.CurrentState == Shared.Mobs.MobState.Dead)
        {
            bloodEfficiency *= ent.Comp.CorpseEfficiency;
        }

        if (TryComp<PerishableComponent>(targetUid, out var rot))
        {
            switch (rot.Stage)
            {
                case 0:
                    bloodEfficiency *= ent.Comp.Rot0Efficiency;
                    break;
                case 1:
                    bloodEfficiency *= ent.Comp.Rot1Efficiency;
                    break;
                case 2:
                    bloodEfficiency *= ent.Comp.Rot2Efficiency;
                    break;
                case 3:
                    bloodEfficiency *= ent.Comp.Rot3Efficiency;
                    break;
                case 4:
                    bloodEfficiency *= ent.Comp.Rot4Efficiency;
                    break;
                default:
                    bloodEfficiency *= ent.Comp.Rot4Efficiency;
                    break;
            }
        }

        if (bloodEfficiency <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-rot"), ent.Owner, ent, PopupType.MediumCaution);
            ent.Comp.IsDrinking = false;
            return;
        }

        var maxCanDrink = ent.Comp.MaxBloodPerTarget - drunkFromTarget;
        var fullSipGain = ent.Comp.BloodGainPerSip * bloodEfficiency;
        var cappedSipGain = MathF.Min(fullSipGain, maxCanDrink);
        if (cappedSipGain <= 0f ||
            ent.Comp.TargetBloodDrainPerSip <= 0f ||
            !TryComp<BloodstreamComponent>(targetUid, out var blood))
        {
            ent.Comp.IsDrinking = false;
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-empty"), ent.Owner, ent, PopupType.MediumCaution);
            return;
        }

        var targetBloodLevel =
            _blood.GetBloodLevel(targetUid) * blood.BloodReferenceSolution.MaxVolume.Value / 100;
        if (targetBloodLevel <= 0f)
        {
            ent.Comp.IsDrinking = false;
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-empty"), ent.Owner, ent, PopupType.MediumCaution);
            return;
        }

        var intendedDrain = ent.Comp.TargetBloodDrainPerSip * (cappedSipGain / fullSipGain);
        var actualDrain = MathF.Min(intendedDrain, targetBloodLevel);
        var actualSipGain = cappedSipGain * (actualDrain / intendedDrain);

        if (_blood.TryModifyBloodLevel(targetUid, -actualDrain))
        {
            AddBlood(ent, actualSipGain, targetUid, countTotalBlood: targetIsHumanoid);

            var biteDamage = new DamageSpecifier();
            biteDamage += new DamageSpecifier(_prototype.Index(PierceTypeId), ent.Comp.BitePierceDamage);
            _damageable.TryChangeDamage(targetUid, biteDamage, ignoreResistances: true);
            _blood.TryModifyBleedAmount(targetUid, ent.Comp.BiteBleedAmount);

            if (TryComp<BlindableComponent>(targetUid, out var blindable))
            {
                var biteCount = ent.Comp.BiteCountsByTarget.GetValueOrDefault(targetUid) + 1;
                if (biteCount >= 3)
                {
                    _blindable.AdjustEyeDamage((targetUid, blindable), 1);
                    biteCount = 0;
                }

                ent.Comp.BiteCountsByTarget[targetUid] = biteCount;
            }

            var healingScale = actualSipGain / ent.Comp.BloodGainPerSip;
            var baseHealSpec = new DamageSpecifier();
            baseHealSpec += new DamageSpecifier(_prototype.Index(BruteGroupId), -ent.Comp.VampHealBrute * healingScale);
            baseHealSpec += new DamageSpecifier(_prototype.Index(BurnGroupId), -ent.Comp.VampHealBurn * healingScale);
            baseHealSpec += new DamageSpecifier(_prototype.Index(PoisonTypeId), -ent.Comp.VampHealPois * healingScale);
            baseHealSpec += new DamageSpecifier(_prototype.Index(OxyLossTypeId), -ent.Comp.VampHealAsphyxiation * healingScale);
            _damageable.TryChangeDamage(ent.Owner, baseHealSpec, true);

            _audio.PlayPvs(BiteSound, targetUid, AudioParams.Default.WithVolume(-7f));
            var targetCoords = Transform(targetUid).Coordinates;
            Spawn("WeaponArcBite", targetCoords);

            var currentDrunkFromTarget = ent.Comp.BloodDrunkFromTargets.GetValueOrDefault(targetUid, 0);
            if (ent.Comp.FangsExtended && currentDrunkFromTarget < ent.Comp.MaxBloodPerTarget)
            {
                ent.Comp.IsDrinking = false;
                StartDrinkDoAfter(ent, targetUid, showPopup: false);
            }
            else
            {
                ent.Comp.IsDrinking = false;
                if (currentDrunkFromTarget >= ent.Comp.MaxBloodPerTarget)
                    _popup.PopupEntity(Loc.GetString("vampire-drink-target-hard-max", ("amount", ent.Comp.MaxBloodPerTarget)), ent.Owner, ent, PopupType.MediumCaution);
            }
        }
        else
        {
            ent.Comp.IsDrinking = false;
            _popup.PopupEntity(Loc.GetString("vampire-drink-target-empty"), ent.Owner, ent, PopupType.MediumCaution);
        }
    }

    partial void UpdateVampireAlert(EntityUid uid)
        => _alerts.ShowAlert(uid, "VampireBlood");

    partial void UpdateVampireFedAlert(Entity<VampireComponent> ent)
    {
        var frac = ent.Comp.MaxBloodFullness <= 0f ? 0f : ent.Comp.BloodFullness / ent.Comp.MaxBloodFullness;
        var sev = (short)Math.Clamp((int)MathF.Ceiling(frac * 4f) + 1, 1, 5);
        _alerts.ShowAlert(ent.Owner, "VampireFed", sev);
    }

    private void StartDrinkDoAfter(Entity<VampireComponent> ent, EntityUid target, bool showPopup)
    {
        if (ent.Comp.IsDrinking)
            return;

        if (IsMouthBlocked(ent.Owner))
        {
            if (showPopup)
                _popup.PopupEntity(Loc.GetString("vampire-mouth-covered"), ent.Owner, ent.Owner);
            return;
        }

        var dargs = new DoAfterArgs(EntityManager,
            ent.Owner,
            ent.Comp.SipInterval,
            new VampireDrinkBloodDoAfterEvent(),
            ent.Owner,
            target)
        {
            DistanceThreshold = ent.Comp.BiteDistanceThreshold,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            AttemptFrequency = AttemptFrequency.StartAndEnd
        };

        if (_doAfter.TryStartDoAfter(dargs))
        {
            ent.Comp.IsDrinking = true;
            if (showPopup)
                _popup.PopupEntity(Loc.GetString("vampire-drink-start", ("target", Identity.Entity(target, EntityManager))), ent.Owner, ent.Owner);
        }
    }

    /// <summary>
	///     On use of action to attempt to sleep a single target; check if target can be slept, if vamp has enough blood, and trigger a doafter
	/// </summary>
    private void OnSleep(Entity<VampireComponent> ent, ref VampireSleepActionEvent args)
    {
        if (args.Handled || !Exists(args.Target))
            return;

        var actionEntity = args.Action.Owner;
        if (!TryGetPowerLevelPrototype(ent.Comp.PowerLevel, out var level))
            return;

        var settings = level.Sleep;
        var target = args.Target;

        if (target == ent.Owner ||
            !_interaction.InRangeAndAccessible(ent.Owner, target, settings.TargetRange))
        {
            return;
        }

        if (IsProtectedByFaith(target) && !settings.IgnoresFaith)
        {
            _popup.PopupEntity(Loc.GetString("vampire-target-protected-by-faith"), ent.Owner, ent.Owner, Shared.Popups.PopupType.MediumCaution);
            return;
        }

        if (HasFlashProtection(target))
        {
            _popup.PopupEntity(Loc.GetString("vampire-sleep-protected"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            return;
        }

        if (HasComp<MindShieldComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("vampire-sleep-shielded"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            return;
        }

        if (!CanSpendBlood(ent, settings.BloodCost))
            return;

        var doAfterEvent = new VampireSleepDoAfterEvent
        {
            Victim = GetNetEntity(target),
            Action = GetNetEntity(actionEntity),
            MaxDistance = settings.BreakRange,
            BloodCost = settings.BloodCost,
            Duration = settings.Duration,
            IgnoresFaith = settings.IgnoresFaith
        };
        var doAfter = new DoAfterArgs(EntityManager, ent.Owner, settings.ChannelTime, doAfterEvent, ent.Owner)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = true,
            MovementThreshold = 0.1f,
            RequireCanInteract = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
            Hidden = true
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _popup.PopupEntity(Loc.GetString(_random.Pick(SleepTargetPopupIds)), target, target, PopupType.SmallCaution);
        args.Handled = true;
    }

    private void OnSleepDoAfterAttempt(Entity<VampireComponent> ent,
        ref DoAfterAttemptEvent<VampireSleepDoAfterEvent> args)
    {
        var target = GetEntity(args.Event.Victim);
        if (!Exists(target) || !_interaction.InRangeAndAccessible(ent.Owner, target, args.Event.MaxDistance))
            args.Cancel();
    }

    private bool HasFlashProtection(EntityUid target)
    {
        var attempt = new FlashAttemptEvent(target, null, null);
        RaiseLocalEvent(target, ref attempt, true);
        return attempt.Cancelled;
    }

    /// <summary>
	///     Triggered once sleep do after is completed, check one more time to see if the target has somehow gained immunity during the do after and if not consume the blood cost and apply the sleep.
    /// </summary>
    private void OnSleepDoAfter(Entity<VampireComponent> ent, ref VampireSleepDoAfterEvent args)
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
            _popup.PopupEntity(Loc.GetString("vampire-target-protected-by-faith"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            RefundSleepAction(args.Action);
            args.Handled = true;
            return;
        }

        if (HasFlashProtection(target))
        {
            _popup.PopupEntity(Loc.GetString("vampire-sleep-protected"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            RefundSleepAction(args.Action);
            args.Handled = true;
            return;
        }

        if (HasComp<MindShieldComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("vampire-sleep-shielded"), ent.Owner, ent.Owner, PopupType.SmallCaution);
            RefundSleepAction(args.Action);
            args.Handled = true;
            return;
        }

        if (!CheckAndConsumeBloodCost(ent, null, args.BloodCost))
        {
            RefundSleepAction(args.Action);
            args.Handled = true;
            return;
        }

        _statusEffects.TryAddStatusEffectDuration(target, SleepingSystem.StatusEffectForcedSleeping, args.Duration);
        args.Handled = true;
    }

    /// <summary>
    /// Возвращает заряд и очищает задержку отмененного гипноза.
    /// </summary>
    private void RefundSleepAction(NetEntity netAction)
    {
        var action = GetEntity(netAction);
        if (!Exists(action) ||
            !TryComp<Content.Shared.Charges.Components.LimitedChargesComponent>(action, out var charges))
        {
            return;
        }

        TryComp<Content.Shared.Charges.Components.AutoRechargeComponent>(action, out var recharge);
        _charges.AddCharges((action, charges, recharge), 1);
        _actions.ClearCooldown(action);
    }

    /// <summary>
    /// Action that stuns nearby mobs for a short duration
    /// </summary>
    private void OnGlare(Entity<VampireComponent> ent, ref VampireGlareActionEvent args)
    {
        if (TryComp<BlindableComponent>(ent, out var blindable) && blindable.IsBlind)
            return;

        if (args.Handled)
            return;

        if (!TryGetPowerLevelPrototype(ent.Comp.PowerLevel, out var level))
            return;

        if (!ent.Comp.ActionEntities.TryGetValue(VampireGlareActionId, out var actionEntity))
            return;

        if (!CheckAndConsumeBloodCost(ent, actionEntity))
            return;

        var settings = level.Glare;
        var targets = _lookup.GetEntitiesInRange(ent.Owner,
            settings.Range,
            LookupFlags.Dynamic | LookupFlags.Sundries);

        var (ourPosition, ourRotation) = _transform.GetWorldPositionRotation(Transform(ent));
        var ourDirection = ourRotation.ToWorldVec();

        foreach (var target in targets)
        {
            if (target == ent.Owner)
                continue;

            var effectScale = HasFlashProtection(target)
                ? settings.FlashProtectionEffectScale
                : 1f;

            if (effectScale <= 0)
                continue;

            var offset = _transform.GetWorldPosition(target) - ourPosition;
            var dot = offset.LengthSquared() > 0f
                ? Vector2.Dot(ourDirection, Vector2.Normalize(offset))
                : 0f;

            if (!TryComp<StaminaComponent>(target, out var stam))
                continue;

            var knockedDown = HasComp<KnockedDownComponent>(target);

            if (dot > 0.7f && !knockedDown)
            {
                _stun.TryAddParalyzeDuration(target, settings.FrontParalyzeDuration * effectScale);

                _stamina.TakeStaminaDamage(target, settings.StaminaDamage * effectScale, stam, source: ent.Owner);

                TryInjectMuteToxin(target, settings.MuteToxinAmount * effectScale);
            }
            else if (dot < -0.7f && !knockedDown)
            {
                _stamina.TakeStaminaDamage(target,
                    settings.StaminaDamage * effectScale,
                    stam,
                    source: ent.Owner);
            }
            else
            {
                _stun.TryAddParalyzeDuration(target, settings.SideParalyzeDuration * effectScale);

                _stamina.TakeStaminaDamage(target,
                    settings.StaminaDamage * effectScale,
                    stam,
                    source: ent.Owner);
            }
        }

        args.Handled = true;
    }

    private bool TryInjectMuteToxin(EntityUid target, float amount)
    {
        if (amount <= 0f)
            return false;

        var solution = new Solution();
        solution.AddReagent(MuteToxinReagentId, FixedPoint2.New(amount));

        if (!_solution.TryGetInjectableSolution(target, out var targetSolution, out _))
            return false;

        return _solution.TryAddSolution(targetSolution.Value, solution);
    }

    private void OnRejuvenateI(Entity<VampireComponent> ent, ref VampireRejuvenateIActionEvent args)
    {
        if (args.Handled)
            return;

        if (!ent.Comp.ActionEntities.TryGetValue(VampireRejuvenateIActionId, out var actionEntity))
            return;

        if (!CheckAndConsumeBloodCost(ent, actionEntity))
            return;

        RemoveRejuvenateStuns(ent.Owner);

        args.Handled = true;
    }

    private void OnRejuvenateII(Entity<VampireComponent> ent, ref VampireRejuvenateIIActionEvent args)
    {
        if (args.Handled)
            return;

        if (!ent.Comp.ActionEntities.TryGetValue(VampireRejuvenateIIActionId, out var actionEntity))
            return;

        if (!CheckAndConsumeBloodCost(ent, actionEntity))
            return;

        if (!TryGetPowerLevelPrototype(ent.Comp.PowerLevel, out var level))
            return;

        var settings = level.Rejuvenation;

        RemoveRejuvenateStuns(ent.Owner);
        PurgeRejuvenateReagents(ent.Owner, settings.ReagentPurgeAmount);
        StartRejuvenateHealing(ent.Owner, settings);

        args.Handled = true;
    }

    private void RemoveRejuvenateStuns(EntityUid uid)
    {
        _statusEffects.TryRemoveStatusEffect(uid, SharedStunSystem.StunId);
        _stun.TryUnstun(uid);
        RemComp<KnockedDownComponent>(uid);
    }

    private void PurgeRejuvenateReagents(EntityUid uid, float configuredAmount)
    {
        var purgeAmount = FixedPoint2.New(configuredAmount);
        if (purgeAmount <= FixedPoint2.Zero)
            return;

        if (!TryComp<BloodstreamComponent>(uid, out var blood))
            return;

        if (!_solution.ResolveSolution(uid, blood.BloodSolutionName, ref blood.BloodSolution, out var bloodstreamSolution))
            return;

        var solEnt = blood.BloodSolution.Value;
        var toRemove = FixedPoint2.Zero;

        foreach (var quant in bloodstreamSolution.Contents.ToArray())
        {
            if (toRemove >= purgeAmount)
                break;

            if (!_prototype.TryIndex<ReagentPrototype>(quant.Reagent.Prototype, out var proto))
                continue;

            if (proto.Metabolisms is null)
                continue;

            if (!proto.Metabolisms.Keys.Any(k => k.Id == "Poison"))
                continue;

            var remaining = purgeAmount - toRemove;
            var removeAmt = FixedPoint2.Min(quant.Quantity, remaining);

            _solution.RemoveReagent(solEnt, quant.Reagent, removeAmt);
            toRemove += removeAmt;
        }
    }

    private void StartRejuvenateHealing(EntityUid uid, VampireRejuvenationLevelSettings settings)
    {
        if (settings.HealTicks <= 0)
            return;

        var active = EnsureComp<ActiveVampireRejuvenateComponent>(uid);
        active.TicksRemaining = settings.HealTicks;
        active.TickInterval = settings.HealTickInterval;
        active.NextTick = _timing.CurTime;
        active.HealBrute = FixedPoint2.New(settings.HealBrute);
        active.HealBurn = FixedPoint2.New(settings.HealBurn);
        active.HealPoison = FixedPoint2.New(settings.HealPoison);
        active.HealAsphyxiation = FixedPoint2.New(settings.HealAsphyxiation);
    }

    private void ProcessActiveRejuvenation(TimeSpan now)
    {
        var rejuvenateQuery = EntityQueryEnumerator<ActiveVampireRejuvenateComponent>();
        while (rejuvenateQuery.MoveNext(out var uid, out var rejuvenate))
        {
            if (now < rejuvenate.NextTick)
                continue;

            ApplyConfiguredHeal(uid, rejuvenate);
            rejuvenate.TicksRemaining--;

            if (rejuvenate.TicksRemaining <= 0)
            {
                RemCompDeferred<ActiveVampireRejuvenateComponent>(uid);
                continue;
            }

            rejuvenate.NextTick = now + rejuvenate.TickInterval;
        }
    }

    private void ApplyConfiguredHeal(EntityUid uid, ActiveVampireRejuvenateComponent rejuvenate)
    {
        var healSpec = new DamageSpecifier();

        if (rejuvenate.HealBrute > FixedPoint2.Zero &&
            _prototype.TryIndex<DamageGroupPrototype>(BruteGroupId, out var brute))
            healSpec += new DamageSpecifier(brute, -rejuvenate.HealBrute);

        if (rejuvenate.HealBurn > FixedPoint2.Zero &&
            _prototype.TryIndex<DamageGroupPrototype>(BurnGroupId, out var burn))
            healSpec += new DamageSpecifier(burn, -rejuvenate.HealBurn);

        if (rejuvenate.HealPoison > FixedPoint2.Zero &&
            _prototype.TryIndex<DamageTypePrototype>(PoisonTypeId, out var poison))
            healSpec += new DamageSpecifier(poison, -rejuvenate.HealPoison);

        if (rejuvenate.HealAsphyxiation > FixedPoint2.Zero &&
            _prototype.TryIndex<DamageTypePrototype>(OxyLossTypeId, out var asphyxiation))
            healSpec += new DamageSpecifier(asphyxiation, -rejuvenate.HealAsphyxiation);

        if (healSpec.Empty)
            return;

        _damageable.TryChangeDamage(uid, healSpec, true);
    }

    #endregion

    #region Прогрессия силы и пассивные эффекты

    /// <summary>
    /// Повышает уровень силы по общему количеству выпитой крови.
    /// Достигнутый уровень никогда не понижается.
    /// </summary>
    private void UpdatePowerLevel(Entity<VampireComponent> ent, bool syncActions = true)
    {
        var uniqueHumanoids = 0;
        foreach (var target in ent.Comp.BloodDrunkFromTargets.Keys)
        {
            if (Exists(target) && HasComp<HumanoidAppearanceComponent>(target))
                uniqueHumanoids++;
        }

        var victimsChanged = ent.Comp.UniqueHumanoidVictims != uniqueHumanoids;
        ent.Comp.UniqueHumanoidVictims = uniqueHumanoids;

        var oldLevel = ent.Comp.PowerLevel;
        var newLevel = oldLevel;

        foreach (var prototype in _prototype.EnumeratePrototypes<VampirePowerLevelPrototype>())
        {
            if (prototype.Level > VampirePowerLevel.Ancient ||
                prototype.Level <= newLevel ||
                prototype.RequiredTotalBlood is not { } requiredTotalBlood ||
                ent.Comp.TotalBlood < requiredTotalBlood)
            {
                continue;
            }

            newLevel = prototype.Level;
        }

        if (newLevel == oldLevel)
        {
            if (victimsChanged)
                Dirty(ent);

            return;
        }

        ent.Comp.PowerLevel = newLevel;
        ApplyPowerLevelSettings(ent);
        Dirty(ent);

        if (oldLevel < VampirePowerLevel.Ancient && newLevel >= VampirePowerLevel.Ancient)
            _popup.PopupEntity(Loc.GetString("vampire-full-power-achieved"), ent.Owner, ent.Owner);

        if (syncActions)
            SyncVampireActions(ent);
    }

    private bool IsMouthBlocked(EntityUid uid)
    {
        if (!HasComp<InventoryComponent>(uid))
            return false;

        var slots = new[] { "mask", "head" };
        foreach (var slot in slots)
            if (_inventory.TryGetSlotEntity(uid, slot, out var ent) &&
                TryComp<IngestionBlockerComponent>(ent.Value, out var blocker) &&
                blocker.Enabled)

                return true;

        return false;
    }

    #endregion
}
