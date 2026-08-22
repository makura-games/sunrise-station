using System.Linq;
using System.Numerics;
using Content.Server._Sunrise.FleshCult.FleshGrowth;
using Content.Server._Sunrise.FleshCult.GameRule;
using Content.Server.Body.Components;
using Content.Server.Traits.Assorted;
using Content.Shared._Sunrise;
using Content.Shared._Sunrise.FleshCult;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Flesh;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Tag;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.FleshCult;

public sealed partial class FleshCultSystem
{
    [Dependency] private FleshCultRuleSystem _fleshCultRule = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;

    private static readonly ProtoId<TagPrototype>[] FleshWallTags = ["Wall", "Flesh"];
    private static readonly ProtoId<TagPrototype>[] FleshSpawnBlockingTags = ["Wall", "Window", "Flesh"];

    public void InitializeHeart()
    {
        SubscribeLocalEvent<FleshHeartComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FleshHeartComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<FleshHeartComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<FleshHeartComponent, DragDropTargetEvent>(HandleDragDropOn);
        SubscribeLocalEvent<FleshHeartComponent, FleshHeartDragFinished>(OnDragFinished);
        SubscribeLocalEvent<FleshHeartComponent, ComponentInit>(OnComponentInit);
    }

    private void OnStartup(EntityUid uid, FleshHeartComponent component, ComponentStartup args)
    {
        _fleshCultRule.StartGameRule();
        var xform = Transform(uid);
        RaiseLocalEvent(new FleshHeartStatusChangeEvent()
        {
            FleshHeartUid = uid,
            OwningStation = xform.GridUid,
            Status = FleshHeartStatus.Base
        });
    }

    private void OnShutdown(EntityUid uid, FleshHeartComponent component, ComponentShutdown args)
    {
        _audioSystem.Stop(component.AmbientAudioStream);
    }

    private void OnDestruction(EntityUid uid, FleshHeartComponent component, DestructionEventArgs args)
    {
        if (component.Status != HeartStatus.Base)
        {
            _audioSystem.Stop(component.AmbientAudioStream);
            var xform = Transform(uid);
            var coordinates = xform.Coordinates;
            foreach (var ent in component.BodyContainer.ContainedEntities.ToArray())
            {
                _containerSystem.Remove(ent, component.BodyContainer, force: true);
                Transform(ent).Coordinates = coordinates;
            }
            var fleshTilesQuery = EntityQueryEnumerator<SpreaderFleshComponent>();
            while (fleshTilesQuery.MoveNext(out var ent, out var comp))
            {
                if (comp.Source != uid)
                    continue;
                if (!TryComp<TagComponent>(ent, out var tagComponent))
                    continue;
                if (_tagSystem.HasAllTags(tagComponent, FleshWallTags))
                    _damageableSystem.TryChangeDamage(ent, component.DamageMobsIfHeartDestruct);
                else
                    QueueDel(ent);

            }
            var fleshWalls = new List<EntityUid>();
            var fleshWallsQuery = EntityQueryEnumerator<TagComponent>();
            while (fleshWallsQuery.MoveNext(out var ent, out var comp))
            {
                if (!TryComp<TagComponent>(ent, out var tagComponent))
                    continue;
                var isFleshWall = _tagSystem.HasAllTags(tagComponent, FleshWallTags);
                if (isFleshWall)
                {
                    fleshWalls.Add(ent);
                }
            }
            foreach (var mob in component.EdgeMobs.ToArray())
            {
                _damageableSystem.TryChangeDamage(mob, component.DamageMobsIfHeartDestruct);
            }
            RaiseLocalEvent(new FleshHeartStatusChangeEvent()
            {
                FleshHeartUid = uid,
                OwningStation = xform.GridUid,
                Status = FleshHeartStatus.Destruction
            });
        }
    }

    public void UpdateHeart(float frameTime)
    {
        var fleshCultRule = EntityQuery<FleshCultRuleComponent>().FirstOrDefault();
        if (fleshCultRule == null)
            return;

        var fleshHeartQuery = EntityQueryEnumerator<FleshHeartComponent, TransformComponent>();
        while (fleshHeartQuery.MoveNext(out var ent, out var comp, out var xform))
        {
            var owningStation = _stationSystem.GetOwningStation(ent, xform);

            if (owningStation != fleshCultRule.TargetStation)
                continue;

            switch (comp.Status)
            {
                case HeartStatus.Base:
                {
                    comp.Accumulator += frameTime;

                    if (comp.Accumulator <= 1)
                        continue;

                    comp.Accumulator -= 1;
                    if (comp.BodyContainer.ContainedEntities.Count >= comp.BodyToFinalStage)
                    {
                        comp.SpawnMobsAccumulator = 500;
                        comp.Status = HeartStatus.Active;
                        RaiseLocalEvent(new FleshHeartStatusChangeEvent()
                        {
                            FleshHeartUid = ent,
                            OwningStation = xform.GridUid,
                            Status = FleshHeartStatus.Active
                        });
                        _pointLight.SetEnabled(ent, true);
                        _chatSystem.DispatchGlobalAnnouncement(
                            Loc.GetString("flesh-heart-activate-warning"),
                            colorOverride: Color.Red);
                        var stationUid = _stationSystem.GetOwningStation(ent);
                        SpawnFleshFloorOnOpenTiles(ent, comp, Transform(ent), 1);
                        _roundEndSystem.CancelRoundEndCountdown(stationUid);
                        _audioSystem.PlayPvs(comp.TransformSound, ent, comp.TransformSound.Params);
                        comp.AmbientAudioStream = _audioSystem.PlayGlobal(
                            "/Audio/_Sunrise/FleshCult/flesh_heart.ogg", Filter.Broadcast(), true,
                            AudioParams.Default.WithLoop(true).WithVolume(-3f))!.Value.Entity;
                        _sharedAppearance.SetData(ent, FleshHeartVisuals.State, FleshHeartStatus.Active);
                    }
                    break;
                }
                case HeartStatus.Active:
                {
                    comp.SpawnMobsAccumulator += frameTime;
                    comp.SpawnObjectsAccumulator += frameTime;
                    comp.FinalStageAccumulator += frameTime;
                    if (comp.SpawnMobsAccumulator >= comp.SpawnMobsFrequency)
                    {
                        comp.SpawnMobsAccumulator = 0;
                        SpawnMonstersOnOpenTiles(comp, xform, comp.SpawnMobsAmount, comp.SpawnMobsRadius);
                    }

                    if (comp.SpawnObjectsAccumulator >= comp.SpawnObjectsFrequency)
                    {
                        comp.SpawnObjectsAccumulator = 0;
                        // SpawnObjectsOnOpenTiles(comp, xform, comp.SpawnObjectsAmount, comp.SpawnObjectsRadius);
                    }

                    if (comp.FinalStageAccumulator >= comp.TimeLiveFinalHeartToWin)
                    {
                        RaiseLocalEvent(new FleshHeartStatusChangeEvent()
                        {
                            FleshHeartUid = ent,
                            OwningStation = owningStation,
                            Status = FleshHeartStatus.Final
                        });
                        comp.Status = HeartStatus.Disable;
                    }
                    break;
                }
                case HeartStatus.Disable:
                {
                    break;
                }
            }
        }
    }

    #region Interaction

    private void HandleDragDropOn(EntityUid uid, FleshHeartComponent component, ref DragDropTargetEvent args)
    {

        if (!CanAbsorb(uid, args.Dragged, component))
        {
            _popup.PopupEntity(Loc.GetString("flesh-heart-cant-absorb-targer"),
                args.User, PopupType.Large);
            return;
        }

        if (!TryComp<FixturesComponent>(args.Dragged, out var fixturesComponent))
        {
            _popup.PopupEntity(Loc.GetString("flesh-heart-cant-absorb-targer"),
                args.User, PopupType.Large);
            return;
        }

        if (fixturesComponent.Fixtures["fix1"].Density <= 60)
        {
            _popup.PopupEntity(
                Loc.GetString("flesh-heart-cant-absorb-targer"),
                uid, PopupType.Large);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.EntryDelay, new FleshHeartDragFinished(), uid, target: args.Dragged, used: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
        };
        _doAfterSystem.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    private void OnComponentInit(EntityUid uid, FleshHeartComponent cryoPodComponent, ComponentInit args)
    {
        cryoPodComponent.BodyContainer = _containerSystem.EnsureContainer<Container>(uid, "bodyContainer");
    }

    private void OnDragFinished(EntityUid uid, FleshHeartComponent component, FleshHeartDragFinished args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        if (!TryComp<FixturesComponent>(args.Args.Target.Value, out var fixturesComponent))
        {
            _popup.PopupEntity(Loc.GetString("flesh-heart-cant-absorb-targer"),
                args.User, PopupType.Large);
            return;
        }

        var xform = Transform(args.Args.Target.Value);

        if (TryComp(args.Args.Target.Value, out ContainerManagerComponent? container))
        {
            foreach (var cont in _containerSystem.GetAllContainers(args.Args.Target.Value, container).ToArray())
            {
                foreach (var ent in cont.ContainedEntities.ToArray())
                {
                    {
                        if (HasComp<OrganComponent>(ent))
                        {
                            continue;
                        }
                        _containerSystem.Remove(ent, cont, force: true);
                        Transform(ent).Coordinates = xform.Coordinates;
                    }
                }
            }
        }

        // SUNRISE-TODO: Убрать конечности хирургией а тело заменить на скелета
        if (TryComp<HumanoidProfileComponent>(args.Args.Target.Value, out var HuAppComponent))
        {
            if (TryComp<BloodstreamComponent>(args.Args.Target.Value, out var bloodstreamComponent))
                _bloodstreamSystem.TryModifyBloodLevel((args.Args.Target.Value, bloodstreamComponent), -300);

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

            _physics.SetDensity(args.Args.Target.Value, "fix1", fixturesComponent.Fixtures["fix1"], 50);

            if (TryComp<AppearanceComponent>(args.Args.Target.Value, out var appComponent))
            {
                _sharedAppearance.SetData(args.Args.Target.Value, DamageVisualizerKeys.Disabled, true, appComponent);
                _damageableSystem.TryChangeDamage(args.Args.Target.Value,
                    new DamageSpecifier() { DamageDict = { { "Slash", 100 } } });
            }

            EnsureComp<UnrevivableComponent>(args.Args.Target.Value);

            _containerSystem.Insert(args.Args.Target.Value, component.BodyContainer, force: true);
            _audioSystem.PlayPvs(component.TransformSound, uid, component.TransformSound.Params);
        }

        args.Handled = true;
    }

    #endregion

    private bool CanAbsorb(EntityUid uid, EntityUid dragged, FleshHeartComponent component)
    {
        if (!TryComp<MobStateComponent>(dragged, out var stateComponent))
            return false;

        if (stateComponent.CurrentState != MobState.Dead)
            return false;

        if (!Transform(uid).Anchored)
            return false;

        if (!TryComp<HumanoidProfileComponent>(dragged, out var humanoidAppearance))
            return false;

        if (!_speciesWhitelist.Contains(humanoidAppearance.Species))
            return false;

        return !TryComp<MindContainerComponent>(dragged, out var mindComp) || true;
    }

    private void SpawnFleshFloorOnOpenTiles(EntityUid fleshHeart, FleshHeartComponent component, TransformComponent xform, float radius)
    {
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        var localpos = xform.Coordinates.Position;
        var tilerefs = _mapSystem.GetLocalTilesIntersecting(xform.GridUid.Value, grid,
            new Box2(localpos + new Vector2(-radius, -radius), localpos + new Vector2(radius, radius))).ToArray();
        foreach (var tileref in tilerefs)
        {
            var canSpawnFloor = true;
            foreach (var ent in _mapSystem.GetAnchoredEntities(xform.GridUid.Value, grid, tileref.GridIndices).ToList())
            {
                if (_tagSystem.HasAnyTag(ent, FleshSpawnBlockingTags))
                    canSpawnFloor = false;
            }
            if (canSpawnFloor)
            {
                var location = _mapSystem.ToCenterCoordinates(tileref, grid);
                var fleshTile = Spawn(component.FleshTileId, location);
                var spreaderFleshComponent = EnsureComp<SpreaderFleshComponent>(fleshTile);
                spreaderFleshComponent.Source = fleshHeart;
            }
        }
    }

    private void SpawnMonstersOnOpenTiles(FleshHeartComponent component, TransformComponent xform, int amount, float radius)
    {
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return;

        var localpos = xform.Coordinates.Position;
        var tilerefs = _mapSystem.GetLocalTilesIntersecting(xform.GridUid.Value, grid,
            new Box2(localpos + new Vector2(-radius, -radius), localpos + new Vector2(radius, radius))).ToArray();
        _random.Shuffle(tilerefs);
        var amountCounter = 0;
        foreach (var tileref in tilerefs)
        {
            var valid = true;
            foreach (var ent in _mapSystem.GetAnchoredEntities((xform.GridUid.Value, grid), tileref.GridIndices))
            {
                if (!_physicsQuery.TryGetComponent(ent, out var body))
                    continue;
                if (body.BodyType != BodyType.Static ||
                    !body.Hard ||
                    (body.CollisionLayer & (int) CollisionGroup.Impassable) == 0)
                    continue;
                valid = false;
                break;
            }
            if (!valid)
                continue;
            amountCounter++;

            var randomMob = _random.Pick(component.Spawns);
            var location = _mapSystem.ToCenterCoordinates(tileref, grid);
            var mob = Spawn(randomMob, location);
            component.EdgeMobs.Add(mob);
            if (amountCounter >= amount)
                return;
        }
    }

    public sealed class FleshHeartStatusChangeEvent : EntityEventArgs
    {
        public EntityUid FleshHeartUid;
        public EntityUid? OwningStation;
        public FleshHeartStatus Status;
    }
}
