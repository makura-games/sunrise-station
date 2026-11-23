using Content.Server.Bed.Cryostorage;
using Content.Server.Body.Components;
using Content.Server.Polymorph.Components;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.SunriseCCVars;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Starlight.Medical.Surgery.Steps.Parts;
using Content.Shared.Station.Components;
using Content.Shared.Zombies;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.CryoTeleport;

public sealed partial class CryoTeleportationSystem : EntitySystem
{
    [Dependency] private CryostorageSystem _cryostorage = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private IPlayerManager _playerMan = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private BodySystem _bodySystem = default!;

    private bool _enable;
    private TimeSpan _transferDelay;
    private TimeSpan _nextTick = TimeSpan.Zero;
    private readonly TimeSpan _refreshCooldown = TimeSpan.FromSeconds(5);

    public override void Initialize()
    {
        _cfg.OnValueChanged(SunriseCCVars.CryoTeleportEnable, OnCryoTeleportEnableChanged, true);
        _cfg.OnValueChanged(SunriseCCVars.CryoTeleportTransferDelay, OnCryoTeleportTransferDelayChanged, true);

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnCompleteSpawn);
        SubscribeLocalEvent<CryoTeleportTargetComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<CryoTeleportTargetComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<CryoTeleportTargetComponent, MobStateChangedEvent>(OnMobStateChanged);
        _playerMan.PlayerStatusChanged += OnSessionStatus;
    }

    private void OnCryoTeleportEnableChanged(bool value)
    {
        _enable = value;
    }

    private void OnCryoTeleportTransferDelayChanged(int value)
    {
        _transferDelay = TimeSpan.FromMinutes(value);
    }

    public override void Update(float delay)
    {
        if (_nextTick > _timing.CurTime)
            return;

        _nextTick += _refreshCooldown;

        if (!_enable)
            return;

        var query = AllEntityQuery<CryoTeleportTargetComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out var mobStateComponent))
        {
            if (comp.Station == null
                || !TryComp<StationCryoTeleportComponent>(comp.Station, out var stationCryoTeleportComponent)
                || !TryComp<StationDataComponent>(comp.Station, out var stationData)
                || mobStateComponent.CurrentState != MobState.Alive
                || comp.ExitTime == null
                || _timing.CurTime - comp.ExitTime < _transferDelay
                || HasComp<CryostorageContainedComponent>(uid)
                || HasComp<ZombieComponent>(uid))
                continue;

            if (HasComp<PolymorphedEntityComponent>(uid))
                continue;

            // Check if the entity has a brain - if no brain, don't teleport to cryo
            // This prevents brainless bodies (e.g., during brain transplant surgery) from being auto-teleported
            if (!_bodySystem.TryGetOrganWithComponent<OrganBrainComponent>(uid, out _))
                continue;

            var stationGrid = _stationSystem.GetLargestGrid((comp.Station.Value, stationData));

            if (stationGrid == null)
                continue;

            var cryoStorage = FindCryoStorage(Transform(stationGrid.Value));

            if (cryoStorage == null)
                continue;

            var ev = new BeforeCryoTeleportEvent();
            RaiseLocalEvent(uid, ev);

            var containedComp = AddComp<CryostorageContainedComponent>(uid);

            containedComp.Cryostorage = cryoStorage.Value;
            containedComp.GracePeriodEndTime = _timing.CurTime;

            var portalCoordinates = _transformSystem.GetMapCoordinates(Transform(uid));

            var portalUid = _entity.SpawnEntity(stationCryoTeleportComponent.PortalPrototype, portalCoordinates);
            _audio.PlayPvs(stationCryoTeleportComponent.TransferSound, portalUid);

            var container = _container.EnsureContainer<ContainerSlot>(cryoStorage.Value, "storage");

            if (_container.Insert(uid, container))
                _cryostorage.HandleEnterCryostorage((uid, containedComp), comp.UserId);
        }
    }

    private void OnCompleteSpawn(PlayerSpawnCompleteEvent ev)
    {
        if (!HasComp<StationCryoTeleportComponent>(ev.Station)
            || ev.JobId == null
            || ev.Player.AttachedEntity == null
            || !_enable
            || HasComp<BorgChassisComponent>(ev.Player.AttachedEntity.Value))
            return;

        var targetComponent = EnsureComp<CryoTeleportTargetComponent>(ev.Player.AttachedEntity.Value);
        targetComponent.Station = ev.Station;
        targetComponent.UserId = ev.Player.UserId;
    }

    private void OnPlayerDetached(EntityUid uid, CryoTeleportTargetComponent comp, PlayerDetachedEvent ev)
    {
        comp.ExitTime ??= _timing.CurTime;
        if (_mind.TryGetMind(uid, out var mindId, out var mind))
            comp.UserId = mind.UserId;
    }

    private void OnPlayerAttached(EntityUid uid, CryoTeleportTargetComponent comp, PlayerAttachedEvent ev)
    {
        var compExitTime = comp.ExitTime;
        if (compExitTime != null)
            comp.ExitTime = null;
        if (_mind.TryGetMind(uid, out var mindId, out var mind))
            comp.UserId = mind.UserId;
    }

    private void OnMobStateChanged(EntityUid uid, CryoTeleportTargetComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            return;

        if (_playerMan.TryGetSessionByEntity(uid, out _))
            component.ExitTime = null;
        else
            component.ExitTime = _timing.CurTime;
    }

    private void OnSessionStatus(object? sender, SessionStatusEventArgs args)
    {
        if (!_enable)
            return;

        if (!TryComp<CryoTeleportTargetComponent>(args.Session.AttachedEntity, out var comp))
            return;

        if (args.Session.Status == SessionStatus.Disconnected && comp.ExitTime == null)
            comp.ExitTime = _timing.CurTime;
        else if (args.Session.Status == SessionStatus.Connected && comp.ExitTime != null)
            comp.ExitTime = null;

        comp.UserId = args.Session.UserId;
    }

    private EntityUid? FindCryoStorage(TransformComponent stationGridTransform)
    {
        var query = AllEntityQuery<CryostorageComponent, TransformComponent>();
        while (query.MoveNext(out var cryoUid, out _, out var cryoTransform))
        {
            if (stationGridTransform.MapUid != cryoTransform.MapUid)
                continue;

            var container = _container.EnsureContainer<ContainerSlot>(cryoUid, "storage");

            if (container.ContainedEntities.Count > 0)
                continue;

            return cryoUid;
        }

        return null;
    }
}
