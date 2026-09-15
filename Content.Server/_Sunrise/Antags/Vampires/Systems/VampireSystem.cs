using Content.Server.Actions;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Objectives.Systems;
using Content.Shared._Sunrise.Antags.Vampires;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires.Components.Classes;
using Content.Shared._Sunrise.Antags.Vampires.Prototypes;
using Content.Shared.Alert;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Server.GameTicking;
using Content.Server.Body.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Prayer;
using Robust.Shared.Audio;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Prometheus;
using Content.Server.Body.Systems;
using Content.Shared._Sunrise.Overlay.Components;
using Content.Server._Sunrise.Objectives.Components;
using Content.Shared._Sunrise.Vampire.Components;

namespace Content.Server._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem : EntitySystem
{
    # region Starlight data collection
    private static readonly Counter _vampireClasses = Metrics.CreateCounter(
        "Vampire_Classes",
        "Numbers of vampire classes chosen by players",
        ["class"]
    );
    #endregion
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private BloodstreamSystem _blood = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private NumberObjectiveSystem _number = default!;
    [Dependency] private IRobustRandom _rand = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    private ISawmill? _sawmill;
    private static readonly ProtoId<DamageGroupPrototype> _bruteGroupId = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> _burnGroupId = "Burn";
    private static readonly ProtoId<DamageGroupPrototype> _geneticGroupId = "Genetic";
    private static readonly ProtoId<DamageTypePrototype> _cellularTypeId = "Cellular";
    private static readonly ProtoId<DamageTypePrototype> _poisonTypeId = "Poison";
    private static readonly ProtoId<DamageTypePrototype> _oxyLossTypeId = "Asphyxiation";
    private static readonly ProtoId<DamageTypePrototype> _heatTypeId = "Heat";
    private static readonly ProtoId<DamageTypePrototype> _pierceTypeId = "Piercing";

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _log.GetSawmill("Vampire");

        SubscribeLocalEvent<VampireComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VampireComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<VampireComponent, VampireProgressionChangedEvent>(OnProgressionChanged);
        SubscribeLocalEvent<ActionsComponent, ComponentStartup>(OnActionsComponentStartup);
        SubscribeLocalEvent<VampireComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<VampireActionUseAttemptEvent>(OnVampireActionUseAttempt);
        InitializeAbilities();
        InitializeObjectives();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<VampireComponent, VampireProgressionComponent>();
        while (query.MoveNext(out var uid, out var comp, out var progression))
        {
            if (now <= progression.NextUpdate)
                continue;

            var elapsed = progression.LastUpdate == TimeSpan.Zero
                ? (float) progression.UpdateDelay.TotalSeconds
                : MathF.Max(0f, (float) (now - progression.LastUpdate).TotalSeconds);

            progression.LastUpdate = now;
            progression.NextUpdate = now + progression.UpdateDelay;

            ProcessBloodDecay((uid, comp), elapsed);
            HandleHolyWater(uid, comp);
            HandleHolyPlace(uid, comp);
        }

        var sunlightQuery = EntityQueryEnumerator<VampireSunlightComponent, TransformComponent>();
        while (sunlightQuery.MoveNext(out var uid, out var sunlight, out var xform))
        {
            if (!TryComp<VampireComponent>(uid, out var vampire))
                continue;

            HandleSpaceExposure(uid, vampire, sunlight, xform);
        }

        ProcessActiveVampireEffects(now);
    }

    private void OnVampireActionUseAttempt(ref VampireActionUseAttemptEvent args)
    {
        args.Allowed = CheckAndConsumeGrantedVampireAction(args.User, args.ActionEntity, args.BloodCost);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!TryComp(ev.Entity, out VampireComponent? vampire))
            return;

        SyncVampireActions(ev.Entity, vampire);
    }

    private void HandleSpaceExposure(EntityUid uid, VampireComponent vampire, VampireSunlightComponent sunlight, TransformComponent xform)
    {
        if (_container.IsEntityInContainer(uid))
        {
            ResetSpaceExposure(sunlight);
            return;
        }

        if (!IsInSpace(xform))
        {
            ResetSpaceExposure(sunlight);
            return;
        }

        if (TryComp<MobStateComponent>(uid, out var mobState) &&
            mobState.CurrentState == Shared.Mobs.MobState.Dead)
        {
            ResetSpaceExposure(sunlight);
            return;
        }

        var now = _timing.CurTime;

        var damageInterval = sunlight.DamageInterval;
        if (damageInterval < TimeSpan.FromSeconds(0.1f))
            damageInterval = TimeSpan.FromSeconds(0.1f);

        if (sunlight.TimeEnteredSpace == null)
        {
            sunlight.TimeEnteredSpace = now;
            sunlight.NextWarningPopup = now + sunlight.GracePeriod;
            sunlight.NextDamageTime = now + sunlight.GracePeriod + damageInterval;
        }

        var timeInSpace = now - sunlight.TimeEnteredSpace.Value;

        if (timeInSpace < sunlight.GracePeriod)
            return;

        if (_timing.CurTime >= sunlight.NextWarningPopup)
        {
            _popup.PopupEntity(Loc.GetString(sunlight.WarningPopup), uid, uid, PopupType.LargeCaution);
            sunlight.NextWarningPopup = _timing.CurTime + sunlight.WarningPopupCooldown;
        }

        var nextDamage = sunlight.NextDamageTime;
        if (nextDamage == null)
        {
            sunlight.NextDamageTime = now + damageInterval;
            nextDamage = sunlight.NextDamageTime;
        }
        if (_timing.CurTime < nextDamage)
            return;

        if (!ProcessSpaceExposureTick(uid, vampire, sunlight))
            return;

        sunlight.NextDamageTime = now + damageInterval;
    }

    private void ResetSpaceExposure(VampireSunlightComponent sunlight)
    {
        sunlight.TimeEnteredSpace = null;
        sunlight.NextDamageTime = null;
        sunlight.NextWarningPopup = TimeSpan.Zero;
    }

    private bool ProcessSpaceExposureTick(EntityUid uid, VampireComponent vampire, VampireSunlightComponent sunlight)
    {
        if (!TryComp<VampireProgressionComponent>(uid, out var progression))
            return false;

        var hadBlood = progression.DrunkBlood > 0;

        if (hadBlood)
            DrainBlood(uid, vampire, sunlight);
        else
        {
            if (!ApplyGeneticSpaceDamage(uid, sunlight))
                return false;
        }

        TryComp<DamageableComponent>(uid, out var damageable);
        TryComp<MobThresholdsComponent>(uid, out var thresholds);
        var healthy = IsAboveHalfHealth(uid, damageable, thresholds);

        var chance = hadBlood ? sunlight.BloodEffectChance : sunlight.BloodlessEffectChance;
        TryApplySpaceDamage(uid, healthy, chance, sunlight);

        return true;
    }

    private void DrainBlood(EntityUid uid, VampireComponent vampire, VampireSunlightComponent sunlight)
    {
        if (!TryComp<VampireProgressionComponent>(uid, out var progression))
            return;

        var drain = Math.Min(sunlight.BloodDrainPerInterval, progression.DrunkBlood);
        if (drain <= 0)
            return;

        TrySpendBlood(uid, vampire, drain, showPopup: false);
    }

    private bool ApplyGeneticSpaceDamage(EntityUid uid, VampireSunlightComponent sunlight)
    {
        if (!_proto.TryIndex<DamageGroupPrototype>(_geneticGroupId, out var damageGroup))
            return true;

        var spec = new DamageSpecifier(damageGroup, sunlight.GeneticDamagePerInterval);
        _damageableSystem.TryChangeDamage(uid, spec, true);

        if (!TryComp(uid, out DamageableComponent? damageable) ||
            damageable == null ||
            !_damageableSystem.GetDamagePerGroup((uid, damageable)).TryGetValue(_geneticGroupId, out var geneticDamage))
        {
            return true;
        }

        _audio.PlayPvs(sunlight.SpaceBurnSound, uid);

        if (geneticDamage < sunlight.GeneticDustThreshold)
            return true;

        DustEntity(uid);
        return false;
    }

    private void TryApplySpaceDamage(EntityUid uid, bool isHealthy, float chance, VampireSunlightComponent sunlight)
    {
        if (!_rand.Prob(Math.Clamp(chance, 0f, 1f)))
            return;

        if (isHealthy)
        {
            if (_proto.TryIndex(_heatTypeId, out var heat))
            {
                var spec = new DamageSpecifier(heat, sunlight.BurnDamage);
                _damageableSystem.TryChangeDamage(uid, spec, true);
            }
        }
        else
        {
            _flammable.AdjustFireStacks(uid, sunlight.FireStacksOnIgnite, ignite: true);
        }

        _audio.PlayPvs(sunlight.SpaceBurnSound, uid);
    }

    private bool IsAboveHalfHealth(EntityUid uid, DamageableComponent? damageable, MobThresholdsComponent? thresholds)
    {
        TryComp<DamageableComponent>(uid, out damageable);
        TryComp<MobThresholdsComponent>(uid, out thresholds);

        if (damageable == null)
            return true;

        if (!_mobThreshold.TryGetDeadThreshold(uid, out var deadThreshold, thresholds) ||
            deadThreshold == null ||
            deadThreshold.Value == FixedPoint2.Zero)
        {
            return true;
        }

        var max = deadThreshold.Value.Float();
        if (max <= 0f)
            return true;

        var current = _damageableSystem.GetTotalDamage((uid, damageable)).Float();
        return current <= max * 0.5f;
    }

    private void DustEntity(EntityUid uid)
    {
        var coords = Transform(uid).Coordinates;
        _popup.PopupEntity(Loc.GetString("admin-smite-turned-ash-other", ("name", uid)), uid, PopupType.LargeCaution);
        QueueDel(uid);
        Spawn("Ash", coords);
    }

    private bool IsInSpace(TransformComponent xform)
    {
        if (xform.GridUid == null)
            return true;

        if (!TryComp(xform.GridUid.Value, out MapGridComponent? grid))
            return true;

        if (!_map.TryGetTileRef(xform.GridUid.Value, grid, xform.Coordinates, out var tileRef))
            return true;

        return _turf.IsSpace(tileRef);
    }

    private bool ProcessBloodDecay(Entity<VampireComponent> ent, float elapsed)
    {
        var (uid, comp) = ent;
        if (!TryComp<VampireBloodDrinkerComponent>(uid, out var drinker)
            || !TryComp<VampireProgressionComponent>(uid, out var progression))
        {
            return false;
        }

        var before = drinker.BloodFullness;
        var wasStarving = before <= 0f;
        var changed = false;

        if (before > 0f)
        {
            drinker.StarvationDrunkBloodDrainAccumulator = 0f;
            drinker.BloodFullness = MathF.Max(0f, before - (drinker.FullnessDecayPerSecond * elapsed));
            changed = !MathF.Abs(drinker.BloodFullness - before).Equals(0f);

            if (changed)
            {
                Dirty(uid, drinker);
                UpdateVampireFedAlert(uid, comp);
            }
        }

        var isStarving = drinker.BloodFullness <= 0f;
        if (wasStarving != isStarving)
            _movementSpeed.RefreshMovementSpeedModifiers(uid);

        // Когда сытость крови пуста, сжигаем запасённую кровь
        if (drinker.BloodFullness <= 0f && drinker.StarvationDrunkBloodDrainPerSecond > 0 && progression.DrunkBlood > 0)
        {
            drinker.StarvationDrunkBloodDrainAccumulator += drinker.StarvationDrunkBloodDrainPerSecond * elapsed;
            var drained = Math.Min(progression.DrunkBlood, (int) drinker.StarvationDrunkBloodDrainAccumulator);
            if (drained <= 0)
                return changed;

            drinker.StarvationDrunkBloodDrainAccumulator -= drained;
            TrySpendBlood(uid, comp, drained, showPopup: false);
            changed = true;
        }

        return changed;
    }

    private void RefreshAllActions(EntityUid uid, VampireComponent comp)
    {
        if (!TryComp<VampireProgressionComponent>(uid, out var progression))
            return;

        foreach (var (_, actionEntity) in comp.ActionEntities)
            TryRefreshVampireAction(uid, actionEntity);
    }

    private void HandleClassSelection(EntityUid uid, VampireComponent comp)
    {
        if (HasChosenClass(uid))
            return;

        var classSelectAction = comp.ClassSelectActionId;

        if (!TryComp<VampireProgressionComponent>(uid, out var progression))
            return;

        if (progression.TotalBlood >= progression.ClassSelectThreshold && !comp.ActionEntities.ContainsKey(classSelectAction))
        {
            EntityUid? actionEntity = null;
            _actions.AddAction(uid, ref actionEntity, classSelectAction, uid);
            if (actionEntity != null)
            {
                comp.ActionEntities[classSelectAction] = actionEntity.Value;
                Dirty(uid, comp);
            }
        }

        if (comp.ActionEntities.TryGetValue(classSelectAction, out var classSelectActionEntity))
            TryRefreshVampireAction(uid, classSelectActionEntity);
    }

    private void OnProgressionChanged(EntityUid uid, VampireComponent comp, ref VampireProgressionChangedEvent args) =>
        SyncVampireActions(uid, comp);

    private void OnActionsComponentStartup(EntityUid uid, ActionsComponent _, ComponentStartup args)
    {
        if (!TryComp(uid, out VampireComponent? vampire))
            return;
        SyncVampireActions(uid, vampire);
    }

    private void SyncVampireActions(EntityUid uid, VampireComponent comp)
    {
        CleanMissingActions(comp);
        HandleClassSelection(uid, comp);
        EnsureRejuvenateUpgrade(uid, comp);
        TryGrantClassAbilities(uid, comp);
        RefreshAllActions(uid, comp);
    }

    private void CleanMissingActions(VampireComponent comp)
    {
        if (comp.ActionEntities.Count == 0)
            return;

        var snapshot = new Dictionary<EntProtoId, EntityUid>(comp.ActionEntities);
        foreach (var pair in snapshot)
        {
            if (Exists(pair.Value))
                continue;

            comp.ActionEntities.Remove(pair.Key);
        }
    }

    private void OnStartup(EntityUid uid, VampireComponent comp, ComponentStartup args)
    {
        EnsureComp<UnholyComponent>(uid);
        EnsureComp<VampireSunlightComponent>(uid);
        EnsureComp<VampireProgressionComponent>(uid);
        EnsureComp<VampireBloodDrinkerComponent>(uid);
        EnsureComp<VampireHealingComponent>(uid);
        EnsureComp<VampireHolyComponent>(uid);
        // Sunrise-Edit: базовые акшены выдаются через грант-компонент (паттерн ActionGrantComponent).
        // Так как вампир превращается после MapInit, инициализируем грант вручную —
        // VampireSystem снимает их при Shutdown автоматически.
        var grant = EnsureComp<VampireActionGrantComponent>(uid);
        grant.Actions.Clear();
        grant.Actions.AddRange(comp.BaseVampireActions);
        foreach (var actionId in comp.BaseVampireActions)
        {
            EntityUid? action = null;

            _actions.AddAction(uid, ref action, actionId, uid);

            if (action != null)
                comp.ActionEntities[actionId] = action.Value;
        }
        RemComp<HungerComponent>(uid);
        RemComp<ThirstComponent>(uid);
        RemComp<RespiratorComponent>(uid);

        _alerts.ClearAlertCategory(uid, "Hunger");

        UpdateVampireAlert(uid);
        UpdateVampireFedAlert(uid, comp);

        SyncVampireActions(uid, comp);
        _movementSpeed.RefreshMovementSpeedModifiers(uid);

    }

    private void OnShutdown(EntityUid uid, VampireComponent comp, ComponentShutdown args)
    {
        RemComp<UnholyComponent>(uid);
        RemComp<StarlightNightVisionComponent>(uid);
        // Sunrise-Edit: снимаем базовые акшены, выданные грант-компонентом
        if (TryComp<VampireActionGrantComponent>(uid, out var grant))
        {
            foreach (var (actionId, actionEntity) in comp.ActionEntities)
            {
                if (grant.Actions.Contains(actionId) && Exists(actionEntity))
                    _actions.RemoveAction(uid, actionEntity);
            }
            RemComp<VampireActionGrantComponent>(uid);
        }
        if (TryComp<VampireDrainBeamComponent>(uid, out var drainBeamComp))
        {
            foreach (var connection in drainBeamComp.ActiveBeams.Values)
            {
                var removeEvent = new VampireDrainBeamEvent(GetNetEntity(connection.Source), GetNetEntity(connection.Target), false, drainBeamComp.VisualPrototype);
                RaiseNetworkEvent(removeEvent);
            }
            drainBeamComp.ActiveBeams.Clear();
        }

        if (TryComp<UmbraeComponent>(uid, out var umbrae))
        {
            umbrae.ShadowBoxingActive = false;
            umbrae.ShadowBoxingTarget = null;
            umbrae.ShadowBoxingEndTime = null;
            umbrae.ShadowBoxingLoopRunning = false;

            if (umbrae.EternalDarknessAuraEntity is { } aura && Exists(aura))
                QueueDel(aura);

            if (umbrae.SpawnedShadowAnchorBeacon is { } beacon && Exists(beacon))
                QueueDel(beacon);

            foreach (var snare in umbrae.PlacedSnares.ToArray())
            {
                if (Exists(snare))
                    QueueDel(snare);
            }

            umbrae.PlacedSnares.Clear();
            umbrae.EternalDarknessAuraEntity = null;
            umbrae.SpawnedShadowAnchorBeacon = null;
            umbrae.ShadowAnchorAutoReturnTime = null;
        }
    }

    partial void UpdateVampireAlert(EntityUid uid);
    partial void UpdateVampireFedAlert(EntityUid uid, VampireComponent? comp);

    private void TryRefreshVampireAction(EntityUid owner, EntityUid? actionEntity)
    {
        if (actionEntity == null
            || _actions.GetAction(actionEntity) is not { } action
            || !TryComp<VampireComponent>(owner, out var vamp)
            || !TryComp<VampireProgressionComponent>(owner, out var progression))
            return;

        if (!TryComp<VampireActionComponent>(actionEntity.Value, out var vac))
        {
            _actions.SetEnabled(action.AsNullable(), true);
            return;
        }

        var enabled = progression.TotalBlood >= vac.BloodToUnlock
             && (vac.RequiredClass == null || ValidateVampireClass(owner, vamp, vac.RequiredClass))
             && (!vac.RequiresFullPower || progression.FullPower);

        _actions.SetEnabled(action.AsNullable(), enabled);
    }

    private void TryGrantClassAbilities(EntityUid uid, VampireComponent comp)
    {
        if (comp.ChosenClassId is not { } classId)
            return;

        if (!_proto.TryIndex(classId, out var classProto))
            return;

        foreach (var actionId in classProto.Actions)
            GrantAbility(uid, comp, actionId);
    }

    private void GrantAbility(EntityUid uid, VampireComponent comp, EntProtoId actionId)
    {
        if (comp.ActionEntities.ContainsKey(actionId))
            return;

        EntityUid? field = null;
        GrantAbility(uid, comp, ref field, actionId);
    }

    private void GrantAbility(EntityUid uid, VampireComponent comp, ref EntityUid? field, EntProtoId actionId)
    {
        if (field != null)
            return;

        if (!TryComp<VampireProgressionComponent>(uid, out var progression))
            return;

        var threshold = GetActionBloodThreshold(actionId);

        if (progression.TotalBlood >= threshold)
        {
            _actions.AddAction(uid, ref field, actionId, uid);
            if (field != null)
            {
                comp.ActionEntities[actionId] = field.Value;
                Dirty(uid, comp);
            }
        }
    }

    private void OnComponentRemove(EntityUid uid, VampireComponent comp, ComponentRemove _)
        => TryRemoveAbilities(uid, comp);

    private void TryRemoveAbilities(EntityUid uid, VampireComponent comp)
    {
        foreach (var (_, action) in comp.ActionEntities)
            _actions.RemoveAction(uid, action);
        comp.ActionEntities.Clear();
        Dirty(uid, comp);
    }

    private int GetActionBloodThreshold(EntProtoId actionId)
    {
        if (_proto.TryIndex<EntityPrototype>(actionId, out var proto) &&
            proto.TryGetComponent<VampireActionComponent>(out var vac, _componentFactory))
            return vac.BloodToUnlock;
        return 0;
    }
    private void EnsureRejuvenateUpgrade(EntityUid uid, VampireComponent comp)
    {
        if (!TryComp<VampireProgressionComponent>(uid, out var progression))
            return;

        if (comp.RejuvenateActions.Count < 2)
        {
            _sawmill?.Error($"Vampire {ToPrettyString(uid)} missing rejuvenate action config");
            return;
        }

        var rejuvenateI = comp.RejuvenateActions[0];
        var rejuvenateII = comp.RejuvenateActions[1];

        var unlockThreshold = GetActionBloodThreshold(rejuvenateII);
        if (progression.TotalBlood < unlockThreshold)
            return;

        if (!comp.ActionEntities.ContainsKey(rejuvenateII))
        {
            EntityUid? action = null;
            _actions.AddAction(uid, ref action, rejuvenateII, uid);
            if (action != null)
                comp.ActionEntities[rejuvenateII] = action.Value;
        }

        TryRefreshVampireAction(uid, comp.ActionEntities[rejuvenateII]);
        if (comp.ActionEntities.TryGetValue(rejuvenateI, out var firstAction))
        {
            _actions.RemoveAction(uid, firstAction);
            comp.ActionEntities.Remove(rejuvenateI);
        }

        Dirty(uid, comp);
    }

    private void HandleHolyWater(EntityUid uid, VampireComponent comp)
    {
        if (!TryComp<VampireProgressionComponent>(uid, out var progression)
            || !TryComp<VampireHolyComponent>(uid, out var holy))
            return;

        if (progression.UniqueHumanoidVictims < 1)
            return;

        if (_timing.CurTime < holy.NextHolyWaterTick)
            return;

        var holywater = _solution.GetTotalPrototypeQuantity(uid, holy.HolyWaterReagentId);
        if (holywater <= FixedPoint2.Zero)
            return;

        if (TryComp(uid, out MobStateComponent? mobState) && mobState.CurrentState == Shared.Mobs.MobState.Dead)
            return;

        holy.NextHolyWaterTick = _timing.CurTime + holy.HolyTickDelay;

        if (progression.DrunkBlood > 0)
        {
            TrySpendBlood(uid, comp, Math.Min(3, progression.DrunkBlood), showPopup: false);

            ApplyGroupDamage(uid, _bruteGroupId, 3f);

            if (TryComp(uid, out StaminaComponent? stamina))
                _stamina.TakeStaminaDamage(uid, 5f, stamina);

            return;
        }

        ApplyGroupDamage(uid, _burnGroupId, 2f);
        if (_rand.Prob(0.25f))
            _flammable.AdjustFireStacks(uid, 2f, ignite: true);
    }

    private void HandleHolyPlace(EntityUid uid, VampireComponent comp)
    {
        if (!TryComp<VampireProgressionComponent>(uid, out var progression)
            || !TryComp<VampireHolyComponent>(uid, out var holy))
            return;

        if (progression.UniqueHumanoidVictims < 1)
            return;

        if (_timing.CurTime < holy.NextHolyPlaceTick)
            return;

        if (!IsInHolyPlace(uid, comp))
            return;

        if (TryComp(uid, out MobStateComponent? mobState) && mobState.CurrentState == Shared.Mobs.MobState.Dead)
            return;

        holy.NextHolyPlaceTick = _timing.CurTime + holy.HolyTickDelay;

        if (_timing.CurTime >= holy.NextHolyPlacePopup)
        {
            _popup.PopupEntity(Loc.GetString("vampire-holy-place-burn"), uid, uid, PopupType.MediumCaution);
            holy.NextHolyPlacePopup = _timing.CurTime + TimeSpan.FromSeconds(5);
        }

        var health = GetApproximateHealth(uid);
        if (health <= 50f)
        {
            _flammable.AdjustFireStacks(uid, 3f, ignite: true);
            return;
        }

        if (_proto.TryIndex<DamageTypePrototype>(_heatTypeId, out var heat))
        {
            var spec = new DamageSpecifier(heat, FixedPoint2.New(3f));
            _damageableSystem.TryChangeDamage(uid, spec, true);
        }
    }

    private bool IsInHolyPlace(EntityUid uid, VampireComponent comp)
    {
        if (!TryComp<VampireHolyComponent>(uid, out var holy))
            return false;

        if (_container.IsEntityInContainer(uid))
            return false;

        var coords = Transform(uid).Coordinates;
        foreach (var ent in _lookup.GetEntitiesInRange(coords, holy.HolyPlaceRange, LookupFlags.Static))
        {
            if (ent == uid)
                continue;

            if (!HasComp<PrayableComponent>(ent))
                continue;

            if (!Transform(ent).Anchored)
                continue;

            if (!_interaction.InRangeUnobstructed(uid, ent, holy.HolyPlaceRange))
                continue;

            return true;
        }

        return false;
    }

    private float GetApproximateHealth(EntityUid uid)
    {
        if (!TryComp(uid, out DamageableComponent? damageable))
            return 100f;

        TryComp(uid, out MobThresholdsComponent? thresholds);

        if (!_mobThreshold.TryGetDeadThreshold(uid, out var deadThreshold, thresholds)
            || deadThreshold == null
            || deadThreshold.Value == FixedPoint2.Zero)
        {
            return 100f - _damageableSystem.GetTotalDamage((uid, damageable)).Float();
        }

        return deadThreshold.Value.Float() - _damageableSystem.GetTotalDamage((uid, damageable)).Float();
    }

    private void ApplyGroupDamage(EntityUid uid, ProtoId<DamageGroupPrototype> groupId, float amount)
    {
        if (!_proto.TryIndex<DamageGroupPrototype>(groupId, out var group))
            return;

        var spec = new DamageSpecifier(group, FixedPoint2.New(amount));
        _damageableSystem.TryChangeDamage(uid, spec, true);
    }

    private void OpenClassUi(EntityUid uid, VampireComponent comp)
    {
        if (comp.ChosenClassId != null)
            return;
        _ui.CloseUi(uid, VampireClassUiKey.Key);
        _ui.OpenUi(uid, VampireClassUiKey.Key, uid);
    }

    private void OnVampireClassChosen(EntityUid uid, VampireComponent comp, VampireClassChosenBuiMsg msg)
    {
        if (comp.ChosenClassId != null)
        {
            _ui.CloseUi(uid, VampireClassUiKey.Key);
            return;
        }

        if (string.IsNullOrWhiteSpace(msg.Choice) || !_proto.TryIndex<VampireClassPrototype>(msg.Choice, out var classProto))
        {
            _ui.CloseUi(uid, VampireClassUiKey.Key);
            return;
        }

        var reg = _componentFactory.GetRegistration(classProto.ClassComponent, ignoreCase: true);
        var classComp = _componentFactory.GetComponent(reg.Type);
        AddComp(uid, classComp);

        EnsureComp<StarlightNightVisionComponent>(uid);

        comp.ChosenClassId = classProto.ID;
        _vampireClasses.WithLabels(classProto.ID).Inc();

        var classSelectAction = comp.ClassSelectActionId;
        if (comp.ActionEntities.TryGetValue(classSelectAction, out var actionEntity))
        {
            _actions.RemoveAction(uid, actionEntity);
            comp.ActionEntities.Remove(classSelectAction);
        }

        _ui.CloseUi(uid, VampireClassUiKey.Key);

        SyncVampireActions(uid, comp);

        Dirty(uid, comp);
    }

    private void OnVampireClassClosed(EntityUid uid, VampireComponent comp, VampireClassClosedBuiMsg _)
    {
        if (comp.ChosenClassId != null)
            return;

        if (TryComp<VampireProgressionComponent>(uid, out var progression))
            _sawmill?.Debug($"Vampire class UI closed without selection for {ToPrettyString(uid)} (blood={progression.TotalBlood})");
    }

    #region Objectives
    private void InitializeObjectives()
        => SubscribeLocalEvent<BloodDrainConditionComponent, ObjectiveGetProgressEvent>(OnBloodDrainGetProgress);

    private void OnBloodDrainGetProgress(EntityUid uid, BloodDrainConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        var target = _number.GetTarget(uid);
        if (args.Mind.OwnedEntity != null
            && TryComp<VampireComponent>(args.Mind.OwnedEntity.Value, out _)
            && TryComp<VampireProgressionComponent>(args.Mind.OwnedEntity.Value, out var vampProgression))
            args.Progress = target > 0 ? MathF.Min(vampProgression.TotalBlood / target, 1f) : 1f;
        else
            args.Progress = 0f;
    }

    #endregion
}
