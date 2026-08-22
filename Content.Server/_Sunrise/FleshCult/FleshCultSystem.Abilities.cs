using System.Linq;
using System.Numerics;
using Content.Server.Body.Components;
using Content.Server.Construction.Components;
using Content.Server.Traits.Assorted;
using Content.Shared._Sunrise;
using Content.Shared._Sunrise.FleshCult;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Audio;
using Robust.Shared.Collections;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.FleshCult;

public sealed partial class FleshCultSystem
{
    private static readonly ProtoId<BodyTypePrototype> SkeletonBodyType = "SkeletonNormal";

    private void InitializeAbilities()
    {
        SubscribeLocalEvent<FleshAbilitiesComponent, FleshCultistHandTransformEvent>(OnHandTransformEvent);
        SubscribeLocalEvent<FleshAbilitiesComponent, FleshCultistBodyTransformEvent>(OnBodyModificationAction);
        SubscribeLocalEvent<FleshAbilitiesComponent, FleshCultistAdrenalinActionEvent>(OnAdrenalinActionEvent);
        SubscribeLocalEvent<FleshAbilitiesComponent, FleshCultistCreateFleshHeartActionEvent>(OnCreateFleshHeartActionEvent);
        SubscribeLocalEvent<FleshAbilitiesComponent, FleshCultistThrowHuggerActionEvent>(OnThrowHugger);
        SubscribeLocalEvent<FleshAbilitiesComponent, FleshCultistAcidSpitActionEvent>(OnAcidSpit);
        SubscribeLocalEvent<FleshAbilitiesComponent, FleshCultistAbsorbBloodPoolActionEvent>(AbsormBloodPool);
        SubscribeLocalEvent<FleshAbilitiesComponent, FleshCultistDevourActionEvent>(OnDevourAction);
        SubscribeLocalEvent<FleshAbilitiesComponent, FleshCultistDevourDoAfterEvent>(OnDevourDoAfter);
        SubscribeLocalEvent<FleshAbilitiesComponent, FleshCultistUnlockAbilityEvent>(OnUnlockAbility);

        SubscribeLocalEvent<FleshAbilitiesComponent, ComponentStartup>(OnStartup);
    }

    private int MatchSaturation(int bloodVolume, bool hasAppearance)
    {
        if (hasAppearance)
        {
            return 80;
        }
        return bloodVolume switch
        {
            >= 300 => 60,
            >= 150 => 40,
            >= 100 => 20,
            _ => 10
        };
    }

    private int MatchEvolutionPoint(int bloodVolume, bool hasAppearance)
    {
        if (hasAppearance)
        {
            return 10;
        }
        return bloodVolume switch
        {
            >= 300 => 5,
            >= 150 => 3,
            >= 100 => 1,
            _ => 0
        };
    }

    private float MatchHealPoint(int bloodVolume, bool hasAppearance)
    {
        if (hasAppearance)
        {
            return 1;
        }
        return bloodVolume switch
        {
            >= 300 => 0.8f,
            >= 150 => 0.6f,
            >= 100 => 0.4f,
            _ => 0.2f
        };
    }

    private void OnUnlockAbility(EntityUid uid, FleshAbilitiesComponent component,
        FleshCultistUnlockAbilityEvent args)
    {
        var action = _action.AddAction(uid, args.Prototype);
        if (action != null)
            component.Actions.Add(action.Value);
    }

    private void OnStartup(EntityUid uid, FleshAbilitiesComponent component, ComponentStartup args)
    {
        foreach (var componentStartingAction in component.StartingActions)
        {
            var action = _action.AddAction(uid, componentStartingAction);
            if (action != null)
                component.Actions.Add(action.Value);
        }
    }

    private void OnDevourAction(EntityUid uid, FleshAbilitiesComponent component, FleshCultistDevourActionEvent args)
    {
        if (args.Handled)
            return;

        var target = args.Target;

        if (!TryComp<MobStateComponent>(target, out var targetState))
            return;
        if (!TryComp<BloodstreamComponent>(target, out var bloodstream))
            return;
        var hasAppearance = false;
        {
            switch (targetState.CurrentState)
            {
                case MobState.Dead:
                    if (TryComp(target, out HumanoidProfileComponent? humanoidAppearance))
                    {
                        if (!_speciesWhitelist.Contains(humanoidAppearance.Species))
                        {
                            _popup.PopupEntity(
                                Loc.GetString("flesh-cultist-devout-target-not-have-flesh"),
                                uid, uid);
                            return;
                        }

                        if (TryComp<FixturesComponent>(target, out var fixturesComponent))
                        {
                            if (fixturesComponent.Fixtures["fix1"].Density <= 60)
                            {
                                _popup.PopupEntity(
                                    Loc.GetString("flesh-cultist-devout-target-invalid"),
                                    uid, uid);
                                return;
                            }
                        }

                        hasAppearance = true;
                    }
                    else
                    {
                        foreach (var reagent in bloodstream.BloodReferenceSolution.Contents)
                        {
                            if (!component.BloodWhitelist.Contains(reagent.Reagent.Prototype))
                            {
                                _popup.PopupEntity(
                                    Loc.GetString("flesh-cultist-devout-target-not-have-flesh"),
                                    uid, uid);
                                return;
                            }
                        }
                        if ((int)bloodstream.BloodReferenceSolution.MaxVolume < 30)
                        {
                            _popup.PopupEntity(
                                Loc.GetString("flesh-cultist-devout-target-invalid"),
                                uid, uid);
                            return;
                        }
                    }
                    var saturation = MatchSaturation((int)bloodstream.BloodReferenceSolution.MaxVolume / 100, hasAppearance);
                    if (TryComp<FleshCultistComponent>(uid, out var fleshCultistComponent) &&
                        fleshCultistComponent.Hunger + saturation >= fleshCultistComponent.MaxHunger)
                    {
                        _popup.PopupEntity(
                            Loc.GetString("flesh-cultist-devout-not-hungry"),
                            uid, uid);
                        return;
                    }
                    _doAfterSystem.TryStartDoAfter(new DoAfterArgs(EntityManager ,uid, component.DevourTime,
                        new FleshCultistDevourDoAfterEvent(), uid, target: target, used: uid)
                    {
                        BreakOnMove = true,
                    });
                    args.Handled = true;
                    break;

                case MobState.Invalid:
                case MobState.Critical:
                case MobState.Alive:
                default:
                    _popup.PopupEntity(
                        Loc.GetString("flesh-cultist-devout-target-alive"),
                        uid, uid);
                    break;
            }
        }
    }

    private void OnDevourDoAfter(EntityUid uid, FleshAbilitiesComponent component, FleshCultistDevourDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (args.Args.Target == null)
            return;

        if (!TryComp<BloodstreamComponent>(args.Args.Target.Value, out var bloodstream))
            return;

        var hasAppearance = false;

        var xform = Transform(args.Args.Target.Value);
        var coordinates = xform.Coordinates;
        _audioSystem.PlayPvs(component.DevourSound, coordinates, AudioParams.Default.WithVariation(0.025f).WithMaxDistance(5f));
        _popup.PopupEntity(Loc.GetString("flesh-cultist-devour-target",
                ("Entity", uid), ("Target", args.Args.Target)), uid);

        if (bloodstream.BloodSolution != null)
        {
            _bloodstreamSystem.SpillAllSolutions((args.Args.Target.Value, bloodstream));
        }

        if (TryComp<HumanoidProfileComponent>(args.Args.Target, out var HuAppComponent))
        {
            if (TryComp(args.Args.Target.Value, out ContainerManagerComponent? container))
            {
                foreach (var cont in _containerSystem.GetAllContainers(args.Args.Target.Value, container).ToArray())
                {
                    foreach (var ent in cont.ContainedEntities.ToArray())
                    {
                        if (HasComp<OrganComponent>(ent))
                        {
                            continue;
                        }
                        _containerSystem.Remove(ent, cont, force: true);
                        Transform(ent).Coordinates = coordinates;
                    }
                }
            }

            // SUNRISE-TODO: Убрать конечности хирургией а тело заменить на скелета
            if (TryComp<BodyComponent>(args.Args.Target, out var body))
            {
                if (body.Organs == null)
                    return;

                foreach (var organ in body.Organs.ContainedEntities)
                {
                    if (!TryComp<OrganComponent>(organ, out var organComp))
                        continue;

                    if (organComp.Category == "Head")
                        continue;

                    QueueDel(organ);
                }
            }

            var bodyType = _prototypeManager.Index(SkeletonBodyType);
            foreach (var (key, data) in bodyType.Layers)
            {
                if (key != HumanoidVisualLayers.Head)
                {
                    _sunriseBody.SetBaseLayerData(args.Args.Target.Value, key, data);
                }
            }

            if (TryComp<FixturesComponent>(args.Args.Target, out var fixturesComponent))
            {
                _physics.SetDensity(args.Args.Target.Value, "fix1", fixturesComponent.Fixtures["fix1"], 50);
            }

            if (TryComp<AppearanceComponent>(args.Args.Target, out var appComponent))
            {
                _sharedAppearance.SetData(args.Args.Target.Value, DamageVisualizerKeys.Disabled, true, appComponent);
            }

            hasAppearance = true;
        }

        var saturation = MatchSaturation((int)bloodstream.BloodReferenceSolution.MaxVolume / 100, hasAppearance);
        var evolutionPoint = MatchEvolutionPoint((int)bloodstream.BloodReferenceSolution.MaxVolume / 100, hasAppearance);
        var healPoint = MatchHealPoint((int)bloodstream.BloodReferenceSolution.MaxVolume / 100, hasAppearance);

        RemComp<BloodstreamComponent>(args.Args.Target.Value);

        EnsureComp<UnrevivableComponent>(args.Args.Target.Value);

        if (!hasAppearance)
        {
            QueueDel(args.Args.Target.Value);
        }

        if (_solutionContainerSystem.TryGetInjectableSolution(uid, out var injectableSolution, out _))
        {
            var transferSolution = new Solution();
            foreach (var solution in component.HealDevourReagents)
            {
                transferSolution.AddReagent(solution.Reagent, solution.Quantity * healPoint);
            }
            _solutionContainerSystem.TryAddSolution(injectableSolution.Value, transferSolution);
        }

        if (TryComp<FleshCultistComponent>(uid, out var fleshCultistComponent))
        {
            fleshCultistComponent.Hunger += saturation;
            _store.TryAddCurrency(new Dictionary<string, FixedPoint2>
                { {StolenMutationPointPrototype, evolutionPoint} }, uid);
        }

    }

    private void AbsormBloodPool(EntityUid uid,
        FleshAbilitiesComponent component,
        FleshCultistAbsorbBloodPoolActionEvent args)
    {
        if (args.Handled)
            return;

        var xform = Transform(uid);
        var puddles = new ValueList<(EntityUid Entity, string Solution)>();
        puddles.Clear();
        foreach (var entity in _lookup.GetEntitiesInRange(xform.MapPosition, 1f))
        {
            if (TryComp<PuddleComponent>(entity, out var puddle))
            {
                puddles.Add((entity, puddle.SolutionName));
            }
        }

        if (puddles.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("flesh-cultist-not-find-puddles"),
                uid, uid, PopupType.Large);
            return;
        }

        var absorbBlood = new Solution();
        foreach (var (puddle, solution) in puddles)
        {
            if (!_solutionContainerSystem.TryGetSolution(puddle, solution, out var puddleSolution))
            {
                continue;
            }
            foreach (var puddleSolutionContent in puddleSolution.Value.Comp.Solution.Contents.ToList())
            {
                if (!component.BloodWhitelist.Contains(puddleSolutionContent.Reagent.Prototype))
                    continue;

                var amount = _solutionContainerSystem.RemoveReagent(
                    puddleSolution.Value,
                    puddleSolutionContent.Reagent,
                    puddleSolutionContent.Quantity);
                if (amount <= FixedPoint2.Zero)
                    continue;

                var blood = new Solution();
                blood.AddReagent(puddleSolutionContent.Reagent, amount);

                absorbBlood.AddSolution(blood, _prototypeManager);
            }
        }

        if (absorbBlood.Volume == 0)
        {
            _popup.PopupEntity(Loc.GetString("flesh-cultist-cant-absorb-puddle"),
                uid, uid, PopupType.Large);
            return;
        }

        _audioSystem.PlayPvs(component.BloodAbsorbSound, uid);
        _popup.PopupEntity(Loc.GetString("flesh-cultist-absorb-puddle", ("Entity", uid)),
            uid, uid, PopupType.Large);

        var transferSolution = new Solution();
        foreach (var solution in component.HealBloodAbsorbReagents)
        {
            transferSolution.AddReagent(solution.Reagent, solution.Quantity * (absorbBlood.Volume / 10));
        }

        if (_solutionContainerSystem.TryGetInjectableSolution(uid, out var injectableSolution, out var _))
        {
            _solutionContainerSystem.TryAddSolution(injectableSolution.Value, transferSolution);
        }
        absorbBlood.RemoveAllSolution();

        args.Handled = true;
    }

    private void OnAcidSpit(EntityUid uid, FleshAbilitiesComponent component, FleshCultistAcidSpitActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var acidBullet = Spawn(component.BulletAcidSpawnId, Transform(uid).Coordinates);
        var xform = Transform(uid);
        var mapCoords = args.Target.ToMap(_entityManager, _transformSystem);
        var direction = mapCoords.Position - xform.MapPosition.Position;
        var userVelocity = _physics.GetMapLinearVelocity(uid);

        _gunSystem.ShootProjectile(acidBullet, direction, userVelocity, uid, uid);
        _audioSystem.PlayPvs(component.SoundBulletAcid, uid, component.SoundBulletAcid.Params);
    }

    private void OnHandTransformEvent(EntityUid uid, FleshAbilitiesComponent component, FleshCultistHandTransformEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<CuffableComponent>(uid, out var cuffableComponent) && cuffableComponent.CuffedHandCount > 0)
        {
            _cuffable.Uncuff(uid, uid, cuffableComponent.Container.ContainedEntities[^1]);
        }

        foreach (var hand in _handsSystem.EnumerateHands(uid))
        {
            var containedEntity = _handsSystem.GetHeldItem(uid, hand);

            if (!TryComp(containedEntity, out MetaDataComponent? metaData) || metaData.EntityPrototype == null)
                continue;

            if (!HasComp<FleshHandModComponent>(containedEntity))
            {
                if (hand != _handsSystem.GetActiveHand(uid))
                    continue;

                var isDrop = _handsSystem.TryDrop(uid, checkActionBlocker: false);
                if (metaData.EntityPrototype.ID == args.Prototype)
                    continue;

                if (isDrop)
                    continue;

                _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-user-hand-blocked"), uid, uid, PopupType.Large);
                return;
            }

            if (metaData.EntityPrototype.ID == args.Prototype)
            {
                _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
                _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-mod-to-hand",
                    ("User", uid), ("Mod", containedEntity)), uid, PopupType.LargeCaution);
                QueueDel(containedEntity);
                EnsureComp<CuffableComponent>(uid);
                args.Handled = true;
                return;
            }
        }

        var modEntity = Spawn(args.Prototype, Transform(uid).Coordinates);
        var isPickup = _handsSystem.TryPickup(uid, modEntity, checkActionBlocker: false, animateUser: false, animate: false);
        if (isPickup)
        {
            _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
            _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-hand-to-mod", ("User", uid), ("Mod", modEntity)), uid, PopupType.LargeCaution);

            // Удаляем компонент наручников, если есть
            if (HasComp<CuffableComponent>(uid))
            {
                RemComp<CuffableComponent>(uid);
            }
        }
        else
        {
            Logger.Error($"Failed to equip {args.Prototype} to hand, removing entity");
            QueueDel(modEntity);
        }

        args.Handled = true;
    }

    private void OnBodyModificationAction(
        EntityUid uid,
        FleshAbilitiesComponent component,
        FleshCultistBodyTransformEvent args)
    {
        if (args.Handled)
            return;

        foreach (var slot in args.CheckSlots)
        {
            if (_inventory.TryGetSlotEntity(uid, slot, out var entity))
            {
                if (HasComp<FleshBodyModComponent>(entity))
                {
                    _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-conflict"),
                        uid, uid, PopupType.Large);
                    return;
                }

               if (_tagSystem.HasAnyTag(entity.Value, args.CheckTags))
               {
                   _inventory.TryUnequip(uid, slot, true, true);
               }
            }
        }

        _inventory.TryGetSlotEntity(uid, args.TargetSlot, out var equippedItem);
        if (equippedItem != null)
        {
            if (TryComp(equippedItem.Value, out MetaDataComponent? metaData) && metaData.EntityPrototype != null &&
                metaData.EntityPrototype.ID == args.Prototype)
            {
                _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
                _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-body-remove",
                    ("User", uid), ("Mod", equippedItem)), uid, PopupType.LargeCaution);
                Del(equippedItem.Value);
                _movement.RefreshMovementSpeedModifiers(uid);
                args.Handled = true;
                return;
            }

            _inventory.TryUnequip(uid, args.TargetSlot, true, true);
        }

        foreach (var slot in args.UnequipSlots)
        {
            _inventory.TryUnequip(uid, slot, true, true);
        }

        var newBodyMod = Spawn(args.Prototype, Transform(uid).Coordinates);
        var equipped = _inventory.TryEquip(uid, newBodyMod, args.TargetSlot, true);
        if (!equipped)
        {
            QueueDel(newBodyMod);
        }
        else
        {
            _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
            _popup.PopupEntity(Loc.GetString("flesh-cultist-transform-body-add",
                ("User", uid), ("Mod", newBodyMod)), uid, PopupType.LargeCaution);
            args.Handled = true;
        }
    }

    private void OnAdrenalinActionEvent(EntityUid uid, FleshAbilitiesComponent component, FleshCultistAdrenalinActionEvent args)
    {
        if (!_solutionContainerSystem.TryGetInjectableSolution(uid, out var injectableSolution, out var _))
            return;
        var transferSolution = new Solution();
        foreach (var solution in component.AdrenalinReagents)
        {
            transferSolution.AddReagent(solution.Reagent, solution.Quantity);
        }
        _solutionContainerSystem.TryAddSolution(injectableSolution.Value, transferSolution);
        args.Handled = true;
    }

    private void OnCreateFleshHeartActionEvent(EntityUid uid, FleshAbilitiesComponent component, FleshCultistCreateFleshHeartActionEvent args)
    {
        var xform = Transform(uid);
        var radius = 1.5f;
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
        {
            _popup.PopupEntity(Loc.GetString("flesh-cultist-cant-spawn-flesh-heart-in-space",
                ("Entity", uid)), uid, PopupType.Large);
            return;
        }

        var offsetValue = Vector2Helpers.Normalized(xform.LocalRotation.ToWorldVec());
        var targetCord = xform.Coordinates.Offset(offsetValue).SnapToGrid(EntityManager);
        var tilerefs = Enumerable.ToArray(_mapSystem.GetLocalTilesIntersecting(xform.GridUid.Value, grid,
            new Box2(targetCord.Position + new Vector2(-radius, -radius), targetCord.Position + new Vector2(radius, radius))));
        foreach (var tileref in tilerefs)
        {
            var tileCoordinates = _mapSystem.GridTileToLocal(xform.GridUid.Value, grid, tileref.GridIndices);
            foreach (var entity in _turf.GetEntitiesInTile(tileCoordinates))
            {
                PhysicsComponent? physics = null; // We use this to check if it's impassable
                if (HasComp<MobStateComponent>(entity) && entity != uid || // Is it a mob?
                    Resolve(entity, ref physics, false) && (physics.CollisionLayer & (int) CollisionGroup.Impassable) != 0 ||
                    HasComp<ConstructionComponent>(entity) && entity != uid) // Is construction?
                {
                    _popup.PopupEntity(Loc.GetString("flesh-cultist-cant-spawn-flesh-heart-here",
                        ("Entity", uid)), uid, PopupType.Large);
                    return;
                }
            }
        }
        _audioSystem.PlayPvs(component.SoundMutation, uid, component.SoundMutation.Params);
        Spawn(component.FleshHeartId, targetCord);
        args.Handled = true;
    }

    private void OnThrowHugger(EntityUid uid, FleshAbilitiesComponent component, FleshCultistThrowHuggerActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var hugger = Spawn(component.HuggerMobSpawnId, Transform(uid).Coordinates);
        var xform = Transform(uid);
        var mapCoords = args.Target.ToMap(_entityManager, _transformSystem);
        var direction = mapCoords.Position - xform.MapPosition.Position;

        _throwing.TryThrow(hugger, direction, 7F, uid, 10F);
        if (component.SoundThrowHugger != null)
        {
            _audioSystem.PlayPvs(component.SoundThrowHugger, uid, component.SoundThrowHugger.Params);
        }
        _popup.PopupEntity(Loc.GetString("flesh-cultist-throw-hugger"), uid, uid,
            PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("flesh-cultist-throw-hugger-others", ("Entity", uid)),
            uid, Filter.PvsExcept(uid), true, PopupType.LargeCaution);
    }

}
