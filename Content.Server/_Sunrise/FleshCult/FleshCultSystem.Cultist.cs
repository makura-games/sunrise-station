using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.Shared._Sunrise.NightVision.Components;
using Content.Shared._Sunrise.CollectiveMind;
using Content.Shared._Sunrise.FleshCult;
using Content.Shared.Actions.Components;
using Content.Shared.Body;
using Content.Shared.Cuffs.Components;
using Content.Shared.Electrocution;
using Content.Shared.FixedPoint;
using Content.Shared.Flash.Components;
using Content.Shared.Humanoid;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.Tag;
using Content.Shared.Temperature.Components;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.FleshCult;

/// <summary>
/// System for managing Flesh Cultist components and their related events and behaviors.
/// </summary>
public sealed partial class FleshCultSystem
{
    [ValidatePrototypeId<CollectiveMindPrototype>]
    private const string FleshCollectiveMindProto = "FleshCult";

    [ValidatePrototypeId<TagPrototype>]
    private const string FleshTagProto = "Flesh";

    private static readonly ProtoId<TagPrototype> FullBodyOuterTag = "FullBodyOuter";

    [ValidatePrototypeId<EntityPrototype>]
    private const string DefaultFleshCultRule = "FleshCult";

    [ValidatePrototypeId<EntityPrototype>]
    private const string CreateFleshHeartObjective = "CreateFleshHeartObjective";

    [ValidatePrototypeId<EntityPrototype>]
    private const string FleshCultSurviveObjective = "FleshCultSurviveObjective";

    [ValidatePrototypeId<CurrencyPrototype>]
    private const string StolenMutationPointPrototype = "StolenMutationPoint";

    private static readonly HumanoidVisualLayers[] FleshSpiderLegLayers =
    [
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LLeg,
        HumanoidVisualLayers.RFoot,
        HumanoidVisualLayers.LFoot,
    ];

    private void InitializeCultist()
    {
        SubscribeLocalEvent<FleshCultistComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FleshCultistComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistNightVisionMutationEvent>(OnNightVisionMutation);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistInsulatedImmunityMutationEvent>(OnInsulatedImmunityMutation);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistPressureImmunityMutationEvent>(OnPressureImmunityMutation);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistFlashImmunityMutationEvent>(OnFlashImmunityMutation);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistRespiratorImmunityMutationEvent>(OnRespiratorImmunityMutation);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistColdTempImmunityMutationEvent>(OnColdTempImmunityMutation);
        SubscribeLocalEvent<FleshCultistComponent, IsEquippingAttemptEvent>(OnBeingEquippedAttempt);
        SubscribeLocalEvent<FleshCultistComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FleshCultistComponent, FleshCultistShopActionEvent>(OnShop);
        SubscribeLocalEvent<FleshCultistComponent, OrganInsertedIntoEvent>(OnOrganInserted);
    }

    private void OnShop(EntityUid uid, FleshCultistComponent component, FleshCultistShopActionEvent args)
    {
        if (TryComp<StoreComponent>(uid, out var store))
            _store.ToggleUi(uid, uid, store);
    }

    private void OnOrganInserted(Entity<FleshCultistComponent> _, ref OrganInsertedIntoEvent args)
    {
        MakeFleshSpiderLegMarkingsHideable(args.Organ);
    }

    private void OnMobStateChanged(EntityUid uid, FleshCultistComponent component, MobStateChangedEvent args)
    {
        switch (args.NewMobState)
        {
            case MobState.Critical:
                HandleCriticalState(uid, component);
                break;
            case MobState.Dead:
                HandleDeadState(uid, component);
                break;
        }
    }

    private void HandleCriticalState(EntityUid uid, FleshCultistComponent component)
    {
        EnsureComp<CuffableComponent>(uid);
        foreach (var hand in _handsSystem.EnumerateHands(uid))
        {
            var item = _handsSystem.GetHeldItem((uid, null), hand);
            if (HasComp<FleshHandModComponent>(item))
            {
                QueueDel(item);
                _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
            }
        }
    }

    private void HandleDeadState(EntityUid uid, FleshCultistComponent component)
    {
        if (component.IsDeathPending)
            return;

        DeleteFleshBodyModComponent(uid, "shoes", component);
        DeleteFleshBodyModComponent(uid, "outerClothing", component);
        ParasiteComesOut(uid, component);
    }

    private void DeleteFleshBodyModComponent(EntityUid uid, string slot, FleshCultistComponent component)
    {
        if (_inventory.TryGetSlotEntity(uid, slot, out var entity) && HasComp<FleshBodyModComponent>(entity))
        {
            Del(entity.Value);
            _movement.RefreshMovementSpeedModifiers(uid);
            _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
        }
    }

    private void OnBeingEquippedAttempt(EntityUid uid, FleshCultistComponent component, IsEquippingAttemptEvent args)
    {
        if (args.Slot is not "outerClothing")
            return;
        _inventory.TryGetSlotEntity(uid, "shoes", out var shoes);
        if (shoes == null)
            return;
        if (!HasComp<FleshBodyModComponent>(shoes))
            return;
        if (!_tagSystem.HasTag(args.Equipment, FullBodyOuterTag))
            return;
        _popup.PopupEntity(Loc.GetString("flesh-cultist-equiped-outer-clothing-blocked",
            ("Entity", uid)), uid, PopupType.Large);
        args.Cancel();
    }

    private void OnStartup(EntityUid uid, FleshCultistComponent component, ComponentStartup args)
    {
        ChangeParasiteHunger(uid, 0, component);
        InitializeActions(uid, component);
        InitializeStore(uid, component);
        InitializeAppearance(uid);
        _store.TryAddCurrency(new Dictionary<string, FixedPoint2> { { StolenMutationPointPrototype, component.StartingMutationPoints } }, uid);
    }

    private void InitializeActions(EntityUid uid, FleshCultistComponent component)
    {
        if (TryComp(uid, out ActionsComponent? actionsComponent))
        {
            _action.AddAction(uid, ref component.ActionFleshCultistShopEntity, component.ActionFleshCultistShop, component: actionsComponent);
            var fleshAbilities = EnsureComp<FleshAbilitiesComponent>(uid);
            var devourAction = _action.AddAction(uid, fleshAbilities.ActionFleshCultistDevourId, component: actionsComponent);
            if (devourAction != null)
                fleshAbilities.Actions.Add(devourAction.Value);
        }
    }

    private void InitializeStore(EntityUid uid, FleshCultistComponent component)
    {
        var storeComp = EnsureComp<StoreComponent>(uid);
        var collectiveMindComponent = EnsureComp<CollectiveMindComponent>(uid);
        if (!collectiveMindComponent.Minds.Contains(FleshCollectiveMindProto))
            collectiveMindComponent.Minds.Add(FleshCollectiveMindProto);

        storeComp.Categories.Add("FleshCultistPassiveSkills");
        storeComp.Categories.Add("FleshCultistActiveSkills");
        storeComp.Categories.Add("FleshCultistWeapon");
        storeComp.Categories.Add("FleshCultistArmor");
        storeComp.CurrencyWhitelist.Add(StolenMutationPointPrototype);
        storeComp.BuySuccessSound = component.BuySuccesSound;
        storeComp.RefundAllowed = false;

        EnsureComp<IgnoreFleshSpiderWebComponent>(uid);
        _tagSystem.AddTag(uid, FleshTagProto);
    }

    private void InitializeAppearance(EntityUid uid)
    {
        EnsureComp<HideableHumanoidLayersComponent>(uid);

        if (!TryComp<BodyComponent>(uid, out var body))
            return;

        foreach (var organ in body.Organs?.ContainedEntities ?? [])
        {
            MakeFleshSpiderLegMarkingsHideable(organ);
        }
    }

    private void MakeFleshSpiderLegMarkingsHideable(EntityUid organ)
    {
        if (!TryComp<VisualOrganMarkingsComponent>(organ, out var markings))
            return;

        var dirty = false;
        foreach (var layer in FleshSpiderLegLayers)
        {
            // Разрешаем одежде скрывать только те слои, которые принадлежат этому органу.
            if (markings.MarkingData.Layers.Contains(layer))
                dirty |= markings.HideableLayers.Add(layer);
        }

        if (dirty)
            DirtyField(organ, markings, nameof(VisualOrganMarkingsComponent.HideableLayers));
    }

    private void OnShutdown(EntityUid uid, FleshCultistComponent component, ComponentShutdown args)
    {
        RemoveActions(uid, component);
        RemoveComponents(uid);
        RemoveCollectiveMind(uid);
        _alerts.ClearAlert(uid, component.MutationPointAlert);
        _tagSystem.RemoveTag(uid, FleshTagProto);
        RemoveObjectives(uid);
    }

    private void RemoveActions(EntityUid uid, FleshCultistComponent component)
    {
        if (TryComp(uid, out ActionsComponent? actionsComponent) && TryComp(uid, out FleshAbilitiesComponent? abilitiesComponent))
        {
            _action.RemoveAction(uid, component.ActionFleshCultistShopEntity);
            foreach (var action in abilitiesComponent.Actions)
            {
                _action.RemoveAction(uid, action);
            }
            abilitiesComponent.Actions.Clear();
        }
    }

    private void RemoveComponents(EntityUid uid)
    {
        RemCompDeferred<IgnoreFleshSpiderWebComponent>(uid);
        RemCompDeferred<InsulatedComponent>(uid);
        RemCompDeferred<FlashImmunityComponent>(uid);
        RemCompDeferred<RespiratorImmunityComponent>(uid);
        RemCompDeferred<PressureImmunityComponent>(uid);
        RemCompDeferred<StoreComponent>(uid);
        RemCompDeferred<FleshAbilitiesComponent>(uid);
    }

    private void RemoveCollectiveMind(EntityUid uid)
    {
        if (TryComp(uid, out CollectiveMindComponent? collectiveMind) && collectiveMind.Minds.Contains(FleshCollectiveMindProto))
            collectiveMind.Minds.Remove(FleshCollectiveMindProto);
    }

    private void RemoveObjectives(EntityUid uid)
    {
        if (_mindSystem.TryGetMind(uid, out var mindId, out var mind))
        {
            if (_mindSystem.TryFindObjective((mindId, mind), CreateFleshHeartObjective, out var fleshHeartObjective))
            {
                _mindSystem.TryRemoveObjective(mindId, mind, mind.Objectives.IndexOf(fleshHeartObjective.Value));
            }

            if (_mindSystem.TryFindObjective((mindId, mind), FleshCultSurviveObjective, out var surviveObjective))
            {
                _mindSystem.TryRemoveObjective(mindId, mind, mind.Objectives.IndexOf(surviveObjective.Value));
            }

            _roles.MindRemoveRole<FleshCultistRoleComponent>((mindId, mind));
        }
    }

    private void OnNightVisionMutation(EntityUid uid, FleshCultistComponent component, FleshCultistNightVisionMutationEvent args)
    {
        EnsureComp<ToggleableNightVisionComponent>(uid);
    }

    private void OnInsulatedImmunityMutation(EntityUid uid, FleshCultistComponent component, FleshCultistInsulatedImmunityMutationEvent args)
    {
        EnsureComp<InsulatedComponent>(uid);
    }

    private void OnPressureImmunityMutation(EntityUid uid, FleshCultistComponent component, FleshCultistPressureImmunityMutationEvent args)
    {
        EnsureComp<PressureImmunityComponent>(uid);
    }

    private void OnFlashImmunityMutation(EntityUid uid, FleshCultistComponent component, FleshCultistFlashImmunityMutationEvent args)
    {
        EnsureComp<FlashImmunityComponent>(uid);
    }

    private void OnRespiratorImmunityMutation(EntityUid uid, FleshCultistComponent component, FleshCultistRespiratorImmunityMutationEvent args)
    {
        EnsureComp<RespiratorImmunityComponent>(uid);
    }

    private void OnColdTempImmunityMutation(EntityUid uid, FleshCultistComponent component, FleshCultistColdTempImmunityMutationEvent args)
    {
        if (TryComp<TemperatureDamageComponent>(uid, out var tempComponent))
            tempComponent.ColdDamageThreshold = 0;
    }

    private bool ChangeParasiteHunger(EntityUid uid, FixedPoint2 amount, FleshCultistComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        component.Hunger += amount;
        if (TryComp<StoreComponent>(uid, out var store))
            _store.UpdateUserInterface(uid, uid, store);

        _alerts.ShowAlert(uid, component.MutationPointAlert, (short)Math.Clamp(Math.Round(component.Hunger.Float() / 10f), 0, 16));
        return true;
    }

    private bool ParasiteComesOut(EntityUid uid, FleshCultistComponent? component = null)
    {
        if (Terminating(uid))
            return false;

        if (!Resolve(uid, ref component))
            return false;

        if (component.IsDeathPending)
            return false;

        component.IsDeathPending = true;
        component.IsTransformationPending = false;

        var coordinates = Transform(uid).Coordinates;
        var abommob = Spawn(component.FleshMutationMobId, _transformSystem.GetMapCoordinates(uid));

        if (_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            _mindSystem.TransferTo(mindId, abommob, ghostCheckOverride: true);

        _popup.PopupEntity(Loc.GetString("flesh-pudge-transform-user", ("EntityTransform", uid)), uid, uid, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("flesh-pudge-transform-others", ("Entity", uid), ("EntityTransform", abommob)), abommob, Filter.PvsExcept(abommob), true, PopupType.LargeCaution);
        _audioSystem.PlayPvs(component.SoundMutation, coordinates, AudioParams.Default.WithVariation(0.025f));

        _gibbing.Gib(uid, true);

        return true;
    }

    public void UpdateCultist(float frameTime)
    {

        // foreach (var cultist in EntityQuery<FleshCultistComponent>())
        var query = EntityQueryEnumerator<FleshCultistComponent>();

        while (query.MoveNext(out var uid, out var cultist))
        {
            if (cultist.IsTransformationPending)
            {
                // Check if entity is still dead and complete the transformation
                if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState == MobState.Dead)
                {
                    ParasiteComesOut(uid, cultist);
                }
                continue;
            }

            cultist.Accumulator += frameTime;
            if (cultist.Accumulator <= 1)
                continue;

            cultist.Accumulator -= 1;
            if (cultist.Hunger <= 40)
            {
                cultist.AccumulatorStarveNotify += 1;
                if (cultist.AccumulatorStarveNotify > 30)
                {
                    cultist.AccumulatorStarveNotify = 0;
                    _popup.PopupEntity(Loc.GetString("flesh-cultist-hungry"), uid, uid, PopupType.Large);
                }
            }

            if (cultist.Hunger < 0)
            {
                // ParasiteComesOut(cultist.Owner, cultist);
                cultist.IsTransformationPending = true;
                ParasiteComesOut(uid, cultist);
                continue;
            }

            ChangeParasiteHunger(uid, cultist.HungerСonsumption, cultist);
        }
    }
}
