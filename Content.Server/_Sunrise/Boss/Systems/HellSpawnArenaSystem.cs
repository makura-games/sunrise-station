using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Content.Server._Sunrise.Boss.Components;
using Content.Server.Atmos.Components;
using Content.Server.Body.Components;
using Content.Server.Chat.Systems;
using Content.Server.Construction;
using Content.Server.RoundEnd;
using Content.Server.Shuttles.Systems;
using Content.Server.Sunrise.Paws;
using Content.Shared._Sunrise.Boss.Components;
using Content.Shared._Sunrise.Boss.Systems;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Events;
using Content.Shared.Construction;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.ForceSay;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.SSDIndicator;
using Content.Shared.Strip.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Numerics;
using Content.Shared.Gibbing;
using Robust.Shared.Maths;

namespace Content.Server._Sunrise.Boss.Systems;

/// <inheritdoc />
public sealed partial class HellSpawnArenaSystem : SharedHellSpawnArenaSystem
{
    [Dependency] private IConsoleHost _console = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private ISawmill _sawmill = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _log.GetSawmill("arena");

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundEnd);
        SubscribeLocalEvent<TravelButtonPressedMessage>(OnTravelButtonPressed);

        SubscribeLocalEvent<HellSpawnFighterComponent, MobStateChangedEvent>(OnFighterMobStateChanged);
        SubscribeLocalEvent<HellSpawnCultistComponent, MobStateChangedEvent>(OnCultistMobStateChanged);
        SubscribeLocalEvent<HellSpawnComponent, MobStateChangedEvent>(OnSpawnMobStateChanged);

        SubscribeLocalEvent<HellSpawnCultistComponent, ComponentShutdown>(OnCultistShutdown);
        SubscribeLocalEvent<HellSpawnCultistComponent, BeingGibbedEvent>(OnCultistGib);

        SubscribeLocalEvent<HellSpawnConsoleComponent, ComponentInit>(OnConsoleInit);
    }

    private void OnConsoleInit(EntityUid uid, HellSpawnConsoleComponent comp, ComponentInit args)
    {
        UpdateArenaUi();
    }

    private async void OnFighterMobStateChanged(EntityUid uid,
        HellSpawnFighterComponent component,
        MobStateChangedEvent args)
    {
        if (args is not { OldMobState: MobState.Alive, NewMobState: MobState.Dead or MobState.Critical })
            return;

        var flag = true;
        var query = EntityQuery<HellSpawnFighterComponent>();
        foreach (var fighter in query)
        {
            if (!_mobState.IsAlive(fighter.Owner))
                continue;
            flag = false;
        }

        if (flag)
        {
            TeleportFightersBack();
            Status = HellSpawnBossStatus.Idle;
            UpdateArenaUi();
            await Task.Delay(1000);
            if (ArenaMap != null)
                _mapManager.DeleteMap(ArenaMap.Value);
        }
    }

    private async void OnCultistMobStateChanged(EntityUid uid,
        HellSpawnCultistComponent component,
        MobStateChangedEvent args)
    {
        if (args is not { OldMobState: MobState.Alive, NewMobState: MobState.Dead or MobState.Critical })
            return;
        if (!SpawnHellSpawn(out var entityUid))
            return;
        Status = HellSpawnBossStatus.InProgress;
        UpdateArenaUi();

        if (TryComp<HellSpawnComponent>(entityUid, out var hellSpawnComponent))
            hellSpawnComponent.ConsoleUid = component.ConsoleUid;
        QueueDel(uid);
    }

    public bool SpawnHellSpawn( [NotNullWhen(true)] out EntityUid? entityUid)
    {
        if (Arena == null)
        {
            entityUid = null;
            return false;
        }

        entityUid = Spawn("MobHellspawnBossSpawn", new EntityCoordinates(Arena.Value, 0.5f, 0.5f));
        var fighters = EntityQuery<HellSpawnFighterComponent>().ToList().Count;
        return true;
    }

    private async void OnCultistShutdown(EntityUid uid, HellSpawnCultistComponent component, ComponentShutdown args)
    {
        if (Status == HellSpawnBossStatus.InProgress)
            return;
        TeleportFightersBack();
        await Task.Delay(1000);
        if (ArenaMap != null && _mapManager.MapExists(ArenaMap))
            _mapManager.DeleteMap(ArenaMap.Value);
    }

    private async void OnCultistGib(EntityUid uid, HellSpawnCultistComponent component, BeingGibbedEvent args)
    {
        if (Status == HellSpawnBossStatus.InProgress)
            return;
        TeleportFightersBack();
        await Task.Delay(1000);
        if (ArenaMap != null)
            _mapManager.DeleteMap(ArenaMap.Value);
    }

    private async void OnSpawnMobStateChanged(EntityUid uid, HellSpawnComponent component, MobStateChangedEvent args)
    {
        if (args is not { OldMobState: MobState.Alive, NewMobState: MobState.Dead or MobState.Critical })
            return;
        TeleportFightersBack();
        Status = HellSpawnBossStatus.Idle;
        UpdateArenaUi();
        if (AllEntityQuery<HellSpawnConsoleComponent, TransformComponent>()
            .MoveNext(out var consoleUid, out _, out _)) // В идеальном случае в мире только один алтарь призыва
            _transform.SetCoordinates(uid, Transform(consoleUid).Coordinates);
        await Task.Delay(1000);
        if (ArenaMap != null)
            _mapManager.DeleteMap(ArenaMap.Value);
    }

    /// <summary>
    /// Телепортируем туда, откуда прилетели
    /// </summary>
    public void TeleportFightersBack()
    {
        var consoleQuery = AllEntityQuery<HellSpawnConsoleComponent>();
        if (!consoleQuery.MoveNext(out var consoleEntity, out _))
            return;
        var cnsXform = Transform(consoleEntity);

        // Для тех, кто тепнулся по-нормальному
        {
            var query = EntityQuery<HellSpawnFighterComponent>();
            foreach (var fighter in query)
            {
                if (fighter.TeleportedFromCoordinates == null)
                {
                    _transform.SetCoordinates(fighter.Owner, cnsXform.Coordinates);
                    continue;
                }

                _transform.SetCoordinates(fighter.Owner, fighter.TeleportedFromCoordinates.Value);
                if (HasComp<HellSpawnFighterComponent>(fighter.Owner))
                    RemComp<HellSpawnFighterComponent>(fighter.Owner);
            }
        }

        // Для заглянувших на огонек гостов
        {
            var query = AllEntityQuery<SpectralComponent>();
            while (query.MoveNext(out var ghostEntity, out _))
            {
                if (ArenaMap != null && _transform.GetMapCoordinates(ghostEntity).MapId == ArenaMap.Value)
                {
                    _transform.SetCoordinates(ghostEntity, cnsXform.Coordinates);
                }
            }
        }

        // Для ААбуз заспавненных существ
        {
            var query = AllEntityQuery<FlammableComponent>();
            while (query.MoveNext(out var flammableEntity, out _))
            {
                if (ArenaMap != null && _transform.GetMapCoordinates(flammableEntity).MapId == ArenaMap.Value)
                {
                    _transform.SetCoordinates(flammableEntity, cnsXform.Coordinates);
                }
            }
        }

        MarkedTargets.Clear();
    }

    /// <summary>
    /// Cleanup после того как раунд закончился
    /// </summary>
    private void OnRoundEnd(RoundRestartCleanupEvent args)
    {
        Status = HellSpawnBossStatus.Idle;
        ArenaMap = null;
        Arena = null;
        Shuttles = new List<EntityUid>();
        MarkedTargets = new HashSet<EntityUid>();
    }

    private void OnTravelButtonPressed(TravelButtonPressedMessage args)
    {
        if (Status == HellSpawnBossStatus.InProgress)
            return;
        var actorUid = args.Actor;
        var ownerUid = GetEntity(args.Owner);
        if (!TryComp<HellSpawnConsoleComponent>(ownerUid, out var consoleComponent))
            return;
        consoleComponent.ActivationTime = _timing.CurTime + CooldownLength;
        var ev = new HellSpawnArenaConsoleUiState
        {
            CurrentStatus = Status,
            ActivationTime = consoleComponent.ActivationTime,
        };
        _ui.SetUiState(ownerUid, HellSpawnArenaUiKey.Key, ev);
        UpdateArenaUi();

        // _sawmill.Debug($"OnTravelButtonPressed: owner: {ownerUid} actor: {actorUid}");

        Cooldown(ownerUid);
    }

    /// <summary>
    /// Проходим по всем консолям и обновляем у них интерфейс.
    /// </summary>
    public void UpdateArenaUi()
    {
        var query = EntityQuery<HellSpawnConsoleComponent>();
        foreach (var cons in query)
        {
            var consoleEv = new HellSpawnArenaConsoleUiState
            {
                CurrentStatus = Status,
                ActivationTime = cons.ActivationTime,
            };
            _ui.SetUiState(cons.Owner, HellSpawnArenaUiKey.Key, consoleEv);
        }
    }

    /// <summary>
    /// Запускаем кд на отправку парней по ту сторону.
    /// </summary>
    /// <param name="console">Консоль, которая хочет отправить туда</param>
    public async void Cooldown(Entity<HellSpawnConsoleComponent?> console)
    {
        if (!Resolve(console.Owner, ref console.Comp))
            return;

        if (Status == HellSpawnBossStatus.Idle)
        {
            var mapId = CreateMap();
            if (mapId == null)
                return;
            _console.ExecuteCommand($"mapinit {mapId}");
            Timer.Spawn(TimeSpan.FromMilliseconds(100), () => OnSpawnCultists());
            Status = HellSpawnBossStatus.Cooldown;
        }

        await Task.Delay(CooldownLength);

        var xform = Transform(console);
        // Серия выборов целей для телепорта
        foreach (var ent in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(xform.Coordinates, 2f))
        {
            MarkedTargets.Add(ent);
        }
        foreach (var ent in _lookup.GetEntitiesInRange<BorgChassisComponent>(xform.Coordinates, 2f))
        {
            MarkedTargets.Add(ent);
        }
        foreach (var ent in _lookup.GetEntitiesInRange<MechComponent>(xform.Coordinates, 2f))
        {
            MarkedTargets.Add(ent);
        }
        foreach (var ent in _lookup.GetEntitiesInRange<FlammableComponent>(xform.Coordinates, 2f))
        {
            MarkedTargets.Add(ent);
        }


        if (Arena == null)
            return;

        foreach (var markedTarget in MarkedTargets)
        {
            var fighterComponent = EnsureComp<HellSpawnFighterComponent>(markedTarget);
            var markedXform = Transform(markedTarget);
            fighterComponent.TeleportedFromCoordinates = markedXform.Coordinates;
            _transform.SetCoordinates(markedTarget, new EntityCoordinates(Arena.Value, 0.5f, -2.5f));
        }
    }

    /// <summary>
    /// Заспавнить культистов(а). Сделано для упрощения изменения поведения босса.
    /// </summary>
    private void OnSpawnCultists()
    {
        if (Arena == null)
            return;

        var center = new EntityCoordinates(Arena.Value, 0.5f, 0.5f);

        var main = Spawn("HellSpawnCultist", center);
        ApplyProtection(main);

        var dummySpawns = new[]
        {
            new EntityCoordinates(Arena.Value, 0.5f, 2.5f),   // north (+2)
            new EntityCoordinates(Arena.Value, 0.5f, -1.5f),  // south (-2)
            new EntityCoordinates(Arena.Value, 2.25f, 0.5f),  // east (+1.75)
            new EntityCoordinates(Arena.Value, -1.25f, 0.5f), // west (-1.75)
        };

        foreach (var coords in dummySpawns)
        {
            var dummy = Spawn("HellSpawnCultistDummy", coords);
            ApplyProtection(dummy);

            var dir = center.Position - coords.Position;
            _transform.SetLocalRotation(dummy, Angle.FromWorldVec(dir));
        }
    }

    /// <summary>
    /// Серия защит от дурачков, ломающих скрипт.
    /// </summary>
    /// <param name="uid">Сущность, которую защищать</param>
    public void ApplyProtection(EntityUid uid)
    {
        if (HasComp<SSDIndicatorComponent>(uid))
            RemComp<SSDIndicatorComponent>(uid);
        if (HasComp<PawsComponent>(uid))
            RemComp<PawsComponent>(uid);
        if (HasComp<PullableComponent>(uid))
            RemComp<PullableComponent>(uid);
        if (HasComp<StrippableComponent>(uid))
            RemComp<StrippableComponent>(uid);
        if (HasComp<StaminaComponent>(uid))
            RemComp<StaminaComponent>(uid);
        if (HasComp<PullableComponent>(uid))
            RemComp<PullableComponent>(uid);
        if (HasComp<DamageForceSayComponent>(uid))
            RemComp<DamageForceSayComponent>(uid);
        // Не надо ебать культиста
        // При мердже на ласт раскомментировать
        // if (HasComp<InteractionComponent>(uid))
        //     RemComp<InteractionComponent>(uid);
        EnsureComp<BlockMovementComponent>(uid);
    }

    public MapId? CreateMap()
    {
        var mapInt = 2000;
        while (_mapManager.MapExists(new MapId(mapInt)))
        {
            mapInt += 1;
        }

        MapId map = new(mapInt);
        _loader.TryLoadMapWithId(map, ShuttlePath, out var mapComponent, out var shuttleUids);

        if (shuttleUids is null)
            return null;
        var shuttleId = shuttleUids.FirstOrNull();
        if (shuttleId == null)
            return null;
        var arenaComp = EnsureComp<HellSpawnArenaComponent>(shuttleId.Value.Owner);
        Arena = shuttleId;
        ArenaMap = map;
        return map;
    }
}
