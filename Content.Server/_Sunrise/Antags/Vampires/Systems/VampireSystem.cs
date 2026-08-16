using Content.Server.Actions;
using Content.Server.Antag;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Objectives.Systems;
using Content.Server._Sunrise.Antags.Vampires.Components;
using Content.Shared._Sunrise.Antags.Vampires;
using Content.Shared._Sunrise.Antags.Vampires.Components;
using Content.Shared.Alert;
using Content.Shared.Actions.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Chemistry.Reagent;
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
using Content.Shared.Mind;
using Content.Shared.Movement.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Content.Server.Body.Components;
using Content.Server.GameTicking;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Prayer;
using Content.Shared.Random;
using Robust.Shared.Audio;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Server.Body.Systems;

namespace Content.Server._Sunrise.Antags.Vampires.Systems;

public sealed partial class VampireSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly BloodstreamSystem _blood = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly NumberObjectiveSystem _number = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly ILogManager _log = default!;
    //[Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly MetabolizerSystem _metabolizer = default!;
    [Dependency] private readonly SharedRoleSystem _role = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;

    private const float VampireObjectiveMaxDifficulty = 10f;
    private const string VampireMetabolizerTypeId = "Vampire";

    private static readonly ProtoId<DamageGroupPrototype> BruteGroupId = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroupId = "Burn";
    private static readonly ProtoId<DamageGroupPrototype> GeneticGroupId = "Genetic";
    private static readonly ProtoId<DamageTypePrototype> PoisonTypeId = "Poison";
    private static readonly ProtoId<DamageTypePrototype> OxyLossTypeId = "Asphyxiation";
    private static readonly ProtoId<DamageTypePrototype> HeatTypeId = "Heat";
    private static readonly ProtoId<DamageTypePrototype> PierceTypeId = "Piercing";
    private static readonly ProtoId<ReagentPrototype> MuteToxinReagentId = "MuteToxin";
    private static readonly SoundSpecifier SpaceBurnSound = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");
    private static readonly SoundSpecifier VampireBriefingSound =
        new SoundPathSpecifier("/Audio/_Sunrise/Ambience/Antag/vampire_start.ogg");
    private static readonly EntProtoId VampireMindRoleId = "MindRoleVampire";
    private static readonly EntProtoId VampireKillObjectiveId = "VampireKillRandomPersonObjective";
    private static readonly EntProtoId VampireDrainObjectiveId = "VampireDrainObjective";
    private static readonly EntProtoId VampireFangsActionId = "ActionVampireToggleFangs";
    private static readonly EntProtoId VampireGlareActionId = "ActionVampireGlare";
    private static readonly EntProtoId VampireSleepActionId = "ActionVampireSleep";
    private static readonly EntProtoId VampireRejuvenateIActionId = "ActionVampireRejuvenateI";
    private static readonly EntProtoId VampireRejuvenateIIActionId = "ActionVampireRejuvenateII";
    private static readonly ProtoId<WeightedRandomPrototype> VampireStateObjectiveGroupId =
        "VampireObjectiveGroupsStateOnly";
    private static readonly ProtoId<WeightedRandomPrototype> VampireStealObjectiveGroupId =
        "VampireObjectiveGroupsStealOnly";

    private ISawmill? _sawmill;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _log.GetSawmill("Vampire");

        SubscribeLocalEvent<VampireComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<VampireComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ActionsComponent, ComponentStartup>(OnActionsComponentStartup);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<BloodDrainConditionComponent, ObjectiveGetProgressEvent>(OnBloodDrainGetProgress);
        InitializeAbilities();
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!TryComp(ev.Entity, out VampireComponent? vampire))
            return;

        SyncVampireActions((ev.Entity, vampire));
    }

    private void OnBloodDrainGetProgress(Entity<BloodDrainConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var target = _number.GetTarget(ent);
        if (args.Mind.OwnedEntity is not { } body ||
            !TryComp<VampireComponent>(body, out var vampire))
        {
            args.Progress = 0f;
            return;
        }

        args.Progress = target > 0f
            ? MathF.Min(vampire.TotalBlood / target, 1f)
            : 1f;
    }

    /// <summary>
    /// Проверяет, можно ли превратить сущность в вампира.
    /// </summary>
    public bool CanMakeVampire(EntityUid target)
    {
        return Exists(target) &&
               !HasComp<VampireComponent>(target) &&
               _mind.TryGetMind(target, out _, out _);
    }

    /// <summary>
    /// Превращает сущность в вампира, выдаёт роль и цели без создания геймрула.
    /// </summary>
    public bool TryMakeVampire(EntityUid target)
    {
        if (!CanMakeVampire(target))
            return false;

        if (!_mind.TryGetMind(target, out var mindId, out var mind))
            return false;

        _role.MindAddRole(mindId, VampireMindRoleId, mind, silent: true);
        EnsureComp<VampireComponent>(target);

        TryAddVampireObjective((mindId, mind), VampireKillObjectiveId);
        TryAddVampireObjective((mindId, mind), VampireDrainObjectiveId);
        TryAddRandomVampireObjective((mindId, mind), VampireStateObjectiveGroupId);
        TryAddRandomVampireObjective((mindId, mind), VampireStealObjectiveGroupId);

        var briefing = Loc.GetString("vampire-role-greeting");
        _antag.SendBriefing(target, briefing, Color.Yellow, VampireBriefingSound);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<VampireComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now <= comp.NextUpdate)
                continue;

            var elapsed = comp.LastUpdate == TimeSpan.Zero
                ? (float)comp.UpdateDelay.TotalSeconds
                : MathF.Max(0f, (float)(now - comp.LastUpdate).TotalSeconds);

            comp.LastUpdate = now;
            comp.NextUpdate = now + comp.UpdateDelay;

            var ent = (uid, comp);
            ProcessBloodDecay(ent, elapsed);
            HandleHolyWater(ent);
            HandleHolyPlace(ent);
        }

        var sunlightQuery = EntityQueryEnumerator<VampireSunlightComponent, TransformComponent>();
        while (sunlightQuery.MoveNext(out var uid, out var sunlight, out var xform))
        {
            if (!TryComp<VampireComponent>(uid, out var vampire))
                continue;

            HandleSpaceExposure((uid, vampire, sunlight), xform);
        }

        ProcessActiveRejuvenation(now);
    }

    private void HandleSpaceExposure(Entity<VampireComponent, VampireSunlightComponent> ent, TransformComponent xform)
    {
        if (_container.IsEntityInContainer(ent.Owner))
        {
            ResetSpaceExposure(ent.Comp2);
            return;
        }

        if (!IsInSpace(xform))
        {
            ResetSpaceExposure(ent.Comp2);
            return;
        }

        if (TryComp<MobStateComponent>(ent, out var mobState) &&
            mobState.CurrentState == Shared.Mobs.MobState.Dead)
        {
            ResetSpaceExposure(ent.Comp2);
            return;
        }

        var now = _timing.CurTime;

        var damageInterval = ent.Comp2.DamageInterval;
        if (damageInterval < TimeSpan.FromSeconds(0.1f))
            damageInterval = TimeSpan.FromSeconds(0.1f);

        if (ent.Comp2.TimeEnteredSpace is null)
        {
            ent.Comp2.TimeEnteredSpace = now;
            ent.Comp2.NextWarningPopup = now + ent.Comp2.GracePeriod;
            ent.Comp2.NextDamageTime = now + ent.Comp2.GracePeriod + damageInterval;
        }

        var timeInSpace = now - ent.Comp2.TimeEnteredSpace.Value;

        if (timeInSpace < ent.Comp2.GracePeriod)
            return;

        if (_timing.CurTime >= ent.Comp2.NextWarningPopup)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp2.WarningPopup), ent.Owner, ent.Owner, PopupType.LargeCaution);
            ent.Comp2.NextWarningPopup = _timing.CurTime + ent.Comp2.WarningPopupCooldown;
        }

        var nextDamage = ent.Comp2.NextDamageTime;
        if (nextDamage is null)
        {
            ent.Comp2.NextDamageTime = now + damageInterval;
            nextDamage = ent.Comp2.NextDamageTime;
        }
        if (_timing.CurTime < nextDamage)
            return;

        if (!ProcessSpaceExposureTick(ent))
            return;

        ent.Comp2.NextDamageTime = now + damageInterval;
    }

    private void ResetSpaceExposure(VampireSunlightComponent sunlight)
    {
        sunlight.TimeEnteredSpace = null;
        sunlight.NextDamageTime = null;
        sunlight.NextWarningPopup = TimeSpan.Zero;
    }

    private bool ProcessSpaceExposureTick(Entity<VampireComponent, VampireSunlightComponent> ent)
    {
        var hadBlood = ent.Comp1.DrunkBlood > 0;

        if (hadBlood)
            DrainBlood(ent);

        else
        {
            if (!ApplyGeneticSpaceDamage(ent))
                return false;
        }

        var damageable = CompOrNull<DamageableComponent>(ent.Owner);
        var thresholds = CompOrNull<MobThresholdsComponent>(ent.Owner);
        var healthy = IsAboveHalfHealth(ent.Owner, damageable, thresholds);

        var chance = hadBlood ? ent.Comp2.BloodEffectChance : ent.Comp2.BloodlessEffectChance;
        TryApplySpaceDamage(ent, healthy, chance);

        return true;
    }

    private void DrainBlood(Entity<VampireComponent, VampireSunlightComponent> ent)
    {
        var drain = Math.Min(ent.Comp2.BloodDrainPerInterval, ent.Comp1.DrunkBlood);
        if (drain <= 0)
            return;

        TrySpendBlood(ent, drain, showPopup: false);
    }

    private bool ApplyGeneticSpaceDamage(Entity<VampireComponent, VampireSunlightComponent> ent)
    {
        if (!_prototype.TryIndex<DamageGroupPrototype>(GeneticGroupId, out var damageGroup))
            return true;

        var spec = new DamageSpecifier(damageGroup, ent.Comp2.GeneticDamagePerInterval);
        _damageable.TryChangeDamage(ent.Owner, spec, true);

        if (!TryComp(ent, out DamageableComponent? damageable) ||
            damageable is null ||
            !damageable.DamagePerGroup.TryGetValue(GeneticGroupId, out var geneticDamage))
        {
            return true;
        }

        _audio.PlayPvs(SpaceBurnSound, ent.Owner);

        if (geneticDamage < ent.Comp2.GeneticDustThreshold)
            return true;

        DustEntity(ent.Owner);
        return false;
    }

    private void TryApplySpaceDamage(Entity<VampireComponent, VampireSunlightComponent> ent, bool isHealthy, float chance)
    {
        if (!_random.Prob(Math.Clamp(chance, 0f, 1f)))
            return;

        if (isHealthy)
        {
            if (_prototype.TryIndex(HeatTypeId, out var heat))
            {
                var spec = new DamageSpecifier(heat, ent.Comp2.BurnDamage);
                _damageable.TryChangeDamage(ent.Owner, spec, true);
            }
        }
        else
            _flammable.AdjustFireStacks(ent.Owner, ent.Comp2.FireStacksOnIgnite, ignite: true);

        _audio.PlayPvs(SpaceBurnSound, ent.Owner);
    }

    private bool IsAboveHalfHealth(EntityUid uid, DamageableComponent? damageable, MobThresholdsComponent? thresholds)
    {
        damageable ??= CompOrNull<DamageableComponent>(uid);
        thresholds ??= CompOrNull<MobThresholdsComponent>(uid);

        if (damageable is null)
            return true;

        if (!_mobThreshold.TryGetDeadThreshold(uid, out var deadThreshold, thresholds) ||
            deadThreshold is null ||
            deadThreshold.Value == FixedPoint2.Zero)
        {
            return true;
        }

        var max = deadThreshold.Value.Float();
        if (max <= 0f)
            return true;

        var current = damageable.TotalDamage.Float();
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
        if (xform.GridUid is null)
            return true;

        if (!TryComp(xform.GridUid.Value, out MapGridComponent? grid))
            return true;

        if (!_map.TryGetTileRef(xform.GridUid.Value, grid, xform.Coordinates, out var tileRef))
            return true;

        return _turf.IsSpace(tileRef);
    }

    private bool ProcessBloodDecay(Entity<VampireComponent> ent, float elapsed)
    {
        var before = ent.Comp.BloodFullness;
        var wasStarving = before <= 0f;
        var changed = false;

        if (before > 0f && _gameTicker.RunLevel < GameRunLevel.PostRound) // No hunger EOR
        {
            ent.Comp.StarvationDrunkBloodDrainAccumulator = 0f;
            ent.Comp.BloodFullness = MathF.Max(0f, before - (ent.Comp.FullnessDecayPerSecond * elapsed));
            changed = !MathF.Abs(ent.Comp.BloodFullness - before).Equals(0f);

            if (changed)
            {
                Dirty(ent);
                UpdateVampireFedAlert(ent);
            }
        }

        var isStarving = ent.Comp.BloodFullness <= 0f;
        if (wasStarving != isStarving)
            _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);

        // When blood fullness is empty, burn stored blood
        if (ent.Comp.BloodFullness <= 0f && ent.Comp.StarvationDrunkBloodDrainPerSecond > 0 && ent.Comp.DrunkBlood > 0)
        {
            ent.Comp.StarvationDrunkBloodDrainAccumulator += ent.Comp.StarvationDrunkBloodDrainPerSecond * elapsed;
            var drained = Math.Min(ent.Comp.DrunkBlood, (int)ent.Comp.StarvationDrunkBloodDrainAccumulator);
            if (drained <= 0)
                return changed;

            ent.Comp.StarvationDrunkBloodDrainAccumulator -= drained;
            TrySpendBlood(ent, drained, showPopup: false);
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Копирует настройки текущего уровня силы в runtime-компонент вампира.
    /// </summary>
    private void ApplyPowerLevelSettings(Entity<VampireComponent> ent)
    {
        if (!TryGetPowerLevelPrototype(ent.Comp.PowerLevel, out var level))
            return;

        var fangs = level.Fangs;

        ent.Comp.MaxBloodFullness = level.MaxBloodFullness;
        ent.Comp.FullnessDecayPerSecond = level.FullnessDecayPerSecond;
        ent.Comp.BloodFullness = MathF.Min(ent.Comp.BloodFullness, ent.Comp.MaxBloodFullness);

        ent.Comp.SipInterval = fangs.SipInterval;
        ent.Comp.BloodGainPerSip = fangs.BloodGain;
        ent.Comp.TargetBloodDrainPerSip = fangs.TargetBloodDrain;
        ent.Comp.AnimalEfficiency = fangs.AnimalEfficiency;
        ent.Comp.CorpseEfficiency = fangs.CorpseEfficiency;
        ent.Comp.BitePierceDamage = fangs.PierceDamage;
        ent.Comp.BiteBleedAmount = fangs.BleedAmount;
        ent.Comp.BiteDistanceThreshold = fangs.Range;
        ent.Comp.MaxBloodPerTarget = fangs.MaxBloodPerTarget;
        ent.Comp.VampHealBrute = fangs.HealBrute;
        ent.Comp.VampHealBurn = fangs.HealBurn;
        ent.Comp.VampHealPois = fangs.HealPoison;
        ent.Comp.VampHealAsphyxiation = fangs.HealAsphyxiation;

        Dirty(ent);
        UpdateVampireFedAlert(ent);
    }

    private bool TryGetPowerLevelPrototype(
        VampirePowerLevel powerLevel,
        out VampirePowerLevelPrototype prototype)
    {
        foreach (var candidate in _prototype.EnumeratePrototypes<VampirePowerLevelPrototype>())
        {
            if (candidate.Level != powerLevel)
                continue;

            prototype = candidate;
            return true;
        }

        _sawmill?.Error($"Missing vampire power level prototype for {powerLevel}");
        prototype = default!;
        return false;
    }

    private void RefreshAllActions(Entity<VampireComponent> ent)
    {
        foreach (var (actionId, actionEntity) in ent.Comp.ActionEntities)
            TryRefreshVampireAction(ent, actionId, actionEntity);
    }

    private void OnActionsComponentStartup(Entity<ActionsComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp(ent, out VampireComponent? vampire))
            return;
        SyncVampireActions((ent.Owner, vampire));
    }

    private void SyncVampireActions(Entity<VampireComponent> ent)
    {
        CleanMissingActions(ent.Comp);
        EnsureRejuvenateUpgrade(ent);
        RefreshAllActions(ent);
    }

    private void CleanMissingActions(VampireComponent comp)
    {
        if (comp.ActionEntities.Count == 0)
            return;

        var snapshot = new List<KeyValuePair<EntProtoId, EntityUid>>(comp.ActionEntities);
        foreach (var pair in snapshot)
        {
            if (Exists(pair.Value))
                continue;

            comp.ActionEntities.Remove(pair.Key);
        }
    }

    private void OnStartup(Entity<VampireComponent> ent, ref ComponentStartup args)
    {
        EnsureComp<VampireSunlightComponent>(ent);
        SetVampireMetabolism(ent.Owner, enabled: true);
        UpdatePowerLevel(ent, syncActions: false);
        ApplyPowerLevelSettings(ent);

        foreach (var actionId in ent.Comp.BaseVampireActions)
        {
            EntityUid? action = null;

            _actions.AddAction(ent.Owner, ref action, actionId, ent.Owner);

            if (action is not null)
                ent.Comp.ActionEntities[actionId] = action.Value;
        }

        RemComp<HungerComponent>(ent);
        RemComp<ThirstComponent>(ent);
        RemComp<RespiratorComponent>(ent);

        _alerts.ClearAlertCategory(ent.Owner, "Hunger");

        UpdateVampireAlert(ent.Owner);
        UpdateVampireFedAlert(ent);

        SyncVampireActions(ent);
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);

    }

    private void OnShutdown(Entity<VampireComponent> ent, ref ComponentShutdown args)
    {
        SetVampireMetabolism(ent.Owner, enabled: false);
    }

    private void SetVampireMetabolism(EntityUid uid, bool enabled)
    {
        foreach (var (organUid, _) in _body.GetBodyOrgans(uid))
        {
            if (!TryComp<MetabolizerComponent>(organUid, out var metabolizer))
                continue;

            if (enabled)
                _metabolizer.TryAddMetabolizerType(metabolizer, VampireMetabolizerTypeId);
            else
                _metabolizer.TryRemoveMetabolizerType(metabolizer, VampireMetabolizerTypeId);
        }
    }

    partial void UpdateVampireAlert(EntityUid uid);
    partial void UpdateVampireFedAlert(Entity<VampireComponent> ent);

    private void TryRefreshVampireAction(
        Entity<VampireComponent> ent,
        EntProtoId actionId,
        EntityUid? actionEntity)
    {
        if (actionEntity is null)
            return;

        if (_actions.GetAction(actionEntity) is not { } action)
            return;

        if (!TryComp<VampireActionComponent>(actionEntity.Value, out var vac))
        {
            _actions.SetEnabled(action.AsNullable(), true);
            return;
        }

        ConfigureVampireAction(ent, actionId, actionEntity.Value);

        var enabled = ent.Comp.PowerLevel >= vac.RequiredPowerLevel;

        _actions.SetEnabled(action.AsNullable(), enabled);
    }

    /// <summary>
    /// Применяет к action параметры текущего уровня силы и сохраняет прогресс его заряда.
    /// </summary>
    private void ConfigureVampireAction(
        Entity<VampireComponent> ent,
        EntProtoId actionId,
        EntityUid actionEntity,
        VampireActionChargeState? previousChargeState = null)
    {
        if (!TryGetPowerLevelPrototype(ent.Comp.PowerLevel, out var level))
            return;

        VampireActionChargeSettings? chargeSettings = null;

        if (actionId == VampireFangsActionId)
        {
            _actions.SetUseDelay(actionEntity, TimeSpan.FromSeconds(2));
        }
        else if (actionId == VampireGlareActionId)
        {
            chargeSettings = level.Glare.Action;
        }
        else if (actionId == VampireSleepActionId)
        {
            chargeSettings = level.Sleep.Action;

            if (TryComp<VampireActionComponent>(actionEntity, out var vampireAction))
                vampireAction.BloodCost = level.Sleep.BloodCost;

            _actions.SetRange(actionEntity, level.Sleep.TargetRange);
        }
        else if (actionId == VampireRejuvenateIActionId ||
                 actionId == VampireRejuvenateIIActionId)
        {
            chargeSettings = level.Rejuvenation.Action;
        }

        if (chargeSettings is null)
            return;

        _actions.SetUseDelay(actionEntity, chargeSettings.UseDelay);
        ConfigureActionCharges(actionEntity, chargeSettings, previousChargeState);
    }

    /// <summary>
    /// Настраивает заряды action, сохраняя текущий заряд и долю уже прошедшего восстановления.
    /// При увеличении максимума новые ячейки сразу заполняются.
    /// </summary>
    private void ConfigureActionCharges(
        EntityUid actionEntity,
        VampireActionChargeSettings settings,
        VampireActionChargeState? previousChargeState = null)
    {
        if (!TryComp<LimitedChargesComponent>(actionEntity, out var charges) ||
            !TryComp<AutoRechargeComponent>(actionEntity, out var recharge))
        {
            return;
        }

        var previous = previousChargeState ?? CaptureActionChargeState(actionEntity);
        var addedCapacity = Math.Max(0, settings.MaxCharges - previous.MaxCharges);
        var newCharges = Math.Clamp(previous.CurrentCharges + addedCapacity, 0, settings.MaxCharges);

        _charges.SetMaxCharges((actionEntity, charges), settings.MaxCharges);
        _charges.SetCharges((actionEntity, charges), newCharges);
        _charges.SetRechargeDuration(
            (actionEntity, charges, recharge),
            settings.RechargeDuration,
            newCharges < settings.MaxCharges ? previous.RechargeProgress : 0f);
    }

    private VampireActionChargeState CaptureActionChargeState(EntityUid actionEntity)
    {
        if (!TryComp<LimitedChargesComponent>(actionEntity, out var charges) ||
            !TryComp<AutoRechargeComponent>(actionEntity, out var recharge))
        {
            return new VampireActionChargeState(0, 0, 0f);
        }

        var currentCharges = _charges.GetCurrentCharges((actionEntity, charges, recharge));
        var progress = 0f;

        if (currentCharges < charges.MaxCharges && recharge.RechargeDuration > TimeSpan.Zero)
        {
            var elapsed = _timing.CurTime - charges.LastUpdate;
            var elapsedTicks = Math.Max(0L, elapsed.Ticks % recharge.RechargeDuration.Ticks);
            progress = Math.Clamp((float)elapsedTicks / recharge.RechargeDuration.Ticks, 0f, 1f);
        }

        return new VampireActionChargeState(currentCharges, charges.MaxCharges, progress);
    }

    private readonly record struct VampireActionChargeState(
        int CurrentCharges,
        int MaxCharges,
        float RechargeProgress);

    private VampirePowerLevel GetRequiredPowerLevel(EntProtoId actionId)
    {
        if (_prototype.TryIndex<EntityPrototype>(actionId, out var proto) &&
            proto.TryGetComponent<VampireActionComponent>(out var vac, _componentFactory))
            return vac.RequiredPowerLevel;

        return VampirePowerLevel.Neonate;
    }

    private void EnsureRejuvenateUpgrade(Entity<VampireComponent> ent)
    {
        if (ent.Comp.RejuvenateActions.Count < 2)
        {
            _sawmill?.Error($"Vampire {ToPrettyString(ent.Owner)} missing rejuvenate action config");
            return;
        }

        var rejuvenateI = ent.Comp.RejuvenateActions[0];
        var rejuvenateII = ent.Comp.RejuvenateActions[1];

        var requiredPowerLevel = GetRequiredPowerLevel(rejuvenateII);
        if (ent.Comp.PowerLevel < requiredPowerLevel)
            return;

        VampireActionChargeState? previousChargeState = null;
        if (ent.Comp.ActionEntities.TryGetValue(rejuvenateI, out var firstAction))
            previousChargeState = CaptureActionChargeState(firstAction);

        if (!ent.Comp.ActionEntities.ContainsKey(rejuvenateII))
        {
            EntityUid? action = null;
            _actions.AddAction(ent.Owner, ref action, rejuvenateII, ent.Owner);
            if (action is not null)
            {
                ent.Comp.ActionEntities[rejuvenateII] = action.Value;
                ConfigureVampireAction(ent, rejuvenateII, action.Value, previousChargeState);
            }
        }

        if (ent.Comp.ActionEntities.TryGetValue(rejuvenateII, out var secondAction))
            TryRefreshVampireAction(ent, rejuvenateII, secondAction);

        if (ent.Comp.ActionEntities.TryGetValue(rejuvenateI, out firstAction))
        {
            _actions.RemoveAction(ent.Owner, firstAction);
            ent.Comp.ActionEntities.Remove(rejuvenateI);
        }

        Dirty(ent);
    }

    private void HandleHolyWater(Entity<VampireComponent> ent)
    {
        if (ent.Comp.UniqueHumanoidVictims < 1)
            return;

        if (_timing.CurTime < ent.Comp.NextHolyWaterTick)
            return;

        var holywater = _solution.GetTotalPrototypeQuantity(ent.Owner, ent.Comp.HolyWaterReagentId);
        if (holywater <= FixedPoint2.Zero)
            return;

        if (TryComp(ent, out MobStateComponent? mobState) && mobState.CurrentState == Shared.Mobs.MobState.Dead)
            return;

        ent.Comp.NextHolyWaterTick = _timing.CurTime + ent.Comp.HolyTickDelay;

        if (ent.Comp.DrunkBlood > 0)
        {
            TrySpendBlood(ent, Math.Min(3, ent.Comp.DrunkBlood), showPopup: false);

            ApplyGroupDamage(ent.Owner, BruteGroupId, 3f);

            if (TryComp(ent, out StaminaComponent? stamina))
                _stamina.TakeStaminaDamage(ent.Owner, 5f, stamina);

            return;
        }

        ApplyGroupDamage(ent.Owner, BurnGroupId, 2f);
        if (_random.Prob(0.25f))
            _flammable.AdjustFireStacks(ent.Owner, 2f, ignite: true);
    }

    private void HandleHolyPlace(Entity<VampireComponent> ent)
    {
        if (ent.Comp.UniqueHumanoidVictims < 1)
            return;

        if (_timing.CurTime < ent.Comp.NextHolyPlaceTick)
            return;

        if (!IsInHolyPlace(ent))
            return;

        if (TryComp(ent, out MobStateComponent? mobState) && mobState.CurrentState == Shared.Mobs.MobState.Dead)
            return;

        ent.Comp.NextHolyPlaceTick = _timing.CurTime + ent.Comp.HolyTickDelay;

        if (_timing.CurTime >= ent.Comp.NextHolyPlacePopup)
        {
            _popup.PopupEntity(Loc.GetString("vampire-holy-place-burn"), ent.Owner, ent.Owner, PopupType.MediumCaution);
            ent.Comp.NextHolyPlacePopup = _timing.CurTime + TimeSpan.FromSeconds(5);
        }

        var health = GetApproximateHealth(ent.Owner);
        if (health <= 50f)
        {
            _flammable.AdjustFireStacks(ent.Owner, 3f, ignite: true);
            return;
        }

        if (_prototype.TryIndex(HeatTypeId, out var heat))
        {
            var spec = new DamageSpecifier(heat, FixedPoint2.New(3f));
            _damageable.TryChangeDamage(ent.Owner, spec, true);
        }
    }

    private bool IsInHolyPlace(Entity<VampireComponent> ent)
    {
        if (_container.IsEntityInContainer(ent.Owner))
            return false;

        var coords = Transform(ent).Coordinates;
        foreach (var target in _lookup.GetEntitiesInRange(coords, ent.Comp.HolyPlaceRange, LookupFlags.Static))
        {
            if (target == ent.Owner)
                continue;

            if (!HasComp<PrayableComponent>(target))
                continue;

            if (!Transform(target).Anchored)
                continue;

            if (!_interaction.InRangeUnobstructed(ent.Owner, target, ent.Comp.HolyPlaceRange))
                continue;

            return true;
        }

        return false;
    }

    private float GetApproximateHealth(EntityUid uid)
    {
        if (!TryComp(uid, out DamageableComponent? damageable))
            return 100f;

        var fallbackHealth = 100f - damageable.TotalDamage.Float();
        if (!_mobThreshold.TryGetDeadThreshold(uid, out var deadThreshold, CompOrNull<MobThresholdsComponent>(uid)))
            return fallbackHealth;

        if (deadThreshold is null || deadThreshold.Value == FixedPoint2.Zero)
            return fallbackHealth;

        return deadThreshold.Value.Float() - damageable.TotalDamage.Float();
    }

    private void ApplyGroupDamage(EntityUid uid, ProtoId<DamageGroupPrototype> groupId, float amount)
    {
        if (!_prototype.TryIndex(groupId, out var group))
            return;

        var spec = new DamageSpecifier(group, FixedPoint2.New(amount));
        _damageable.TryChangeDamage(uid, spec, true);
    }

    private void TryAddVampireObjective(Entity<MindComponent> mind, EntProtoId objective)
    {
        if (_mind.TryAddObjective(mind, mind.Comp, objective))
            return;

        _sawmill?.Error(
            $"Failed to add vampire objective {objective} to {ToPrettyString(mind.Comp.OwnedEntity)}");
    }

    private void TryAddRandomVampireObjective(
        Entity<MindComponent> mind,
        ProtoId<WeightedRandomPrototype> objectiveGroup)
    {
        if (_objectives.GetRandomObjective(
                mind,
                mind.Comp,
                objectiveGroup,
                VampireObjectiveMaxDifficulty) is { } objective)
        {
            _mind.AddObjective(mind, mind.Comp, objective);
            return;
        }

        _sawmill?.Error(
            $"Failed to select vampire objective from group {objectiveGroup} for {ToPrettyString(mind.Comp.OwnedEntity)}");
    }
}
