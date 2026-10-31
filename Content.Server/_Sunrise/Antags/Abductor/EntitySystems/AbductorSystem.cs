using Content.Server.Actions;
using Content.Server.DoAfter;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.Antags.Abductor;
using Content.Shared.Eye;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Interaction.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.UserInterface;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Content.Shared.Tag;
using Robust.Server.Containers;
using Robust.Shared.Toolshed.TypeParsers;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Antags.Abductor;

// TODO: Рефактор. Эта система огромнейший сборник щиткода с гигантским количеством варнингов. Она ужасна и не должна существовать.
public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private StationSystem _stationSystem = default!;
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private UserInterfaceSystem _uiSystem = default!;
    [Dependency] private SharedEyeSystem _eye = default!;
    [Dependency] private SharedMoverController _mover = default!;
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private TransformSystem _xformSys = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;
    private readonly EntProtoId _nanoStation = "StandardNanotrasenStation";
    public override void Initialize()
    {
        SubscribeLocalEvent<AbductorHumanObservationConsoleComponent, BeforeActivatableUIOpenEvent>(OnBeforeActivatableUIOpen);
        SubscribeLocalEvent<AbductorHumanObservationConsoleComponent, ActivatableUIOpenAttemptEvent>(OnActivatableUIOpenAttemptEvent);

        SubscribeLocalEvent<AbductorComponent, GetVisMaskEvent>(OnAbductorGetVis);

        Subs.BuiEvents<AbductorHumanObservationConsoleComponent>(AbductorCameraConsoleUIKey.Key, subs => subs.Event<AbductorBeaconChosenBuiMsg>(OnAbductorBeaconChosenBuiMsg));

        InitializeActions();
        InitializeGizmo();
        InitializeConsole();
        InitializeOrgans();
        InitializeVest();
        InitializeExtractor();
        base.Initialize();
    }

    private void OnAbductorGetVis(Entity<AbductorComponent> ent, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int)VisibilityFlags.Abductor;
    }

    private void OnAbductorBeaconChosenBuiMsg(Entity<AbductorHumanObservationConsoleComponent> ent, ref AbductorBeaconChosenBuiMsg args)
    {
        OnCameraExit(args.Actor);

        EntityUid eye;

        ent.Comp.Opener = args.Actor;

        if (!TryComp<UserInterfaceComponent>(ent, out var bound))
            return;

        foreach (var uids in bound.Actors.Values)
        {
            foreach (var uid in uids)
            {
                if (uid == args.Actor)
                    continue;

                _uiSystem.CloseUserUis<AbductorCameraConsoleUIKey>(uid);
            }
        }

        var beacon = _entityManager.GetEntity(args.Beacon.NetEnt);
        var beaconCoords = Transform(beacon).Coordinates;

        if (ent.Comp.RemoteEntity != null && TryGetEntity(ent.Comp.RemoteEntity, out var existingEye))
        {
            eye = existingEye.Value;
            _xformSys.SetCoordinates(eye, beaconCoords);
        }
        else
        {
            if (ent.Comp.RemoteEntityProto == null)
                return;

            eye = SpawnAtPosition(ent.Comp.RemoteEntityProto, Transform(beacon).Coordinates);
            ent.Comp.RemoteEntity = GetNetEntity(eye);

            EnsureComp<VisibilityComponent>(eye);

            var remoteEyeSource = EnsureComp<RemoteEyeSourceContainerComponent>(eye);
            remoteEyeSource.Actor = args.Actor;

            Dirty(eye, remoteEyeSource);
        }

        AddVirtualItems(args.Actor, ent);
        SetEye(args.Actor, eye, args);
        Dirty(ent);
    }
    private void OnCameraExit(EntityUid actor)
    {
        if (!HasComp<RelayInputMoverComponent>(actor))
            return;

        EntityUid? console = null;

        if (TryComp<AbductorScientistComponent>(actor, out var scientistComp) && scientistComp.Console is { } scientistConsole)
            console = scientistConsole;

        if (TryComp<AbductorAgentComponent>(actor, out var agentComp) && agentComp.Console is { } agentConsole)
            console = agentConsole;

        if (console == null)
            return;

        if (!TryComp<AbductorHumanObservationConsoleComponent>(console, out var comp))
            return;

        comp.Opener = null;

        RemoveEye(actor);
        _virtualItem.DeleteInHandsMatching(actor, console.Value);
    }

    private void OnActivatableUIOpenAttemptEvent(Entity<AbductorHumanObservationConsoleComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!HasComp<AbductorScientistComponent>(args.User) && !HasComp<AbductorAgentComponent>(args.User))
            args.Cancel();

        if (ent.Comp.Opener != null && ent.Comp.Opener != args.User)
        {
            _popup.PopupEntity(Loc.GetString("console-occupied"), args.User, args.User);
            args.Cancel();
        }
    }

    private void OnBeforeActivatableUIOpen(Entity<AbductorHumanObservationConsoleComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        if (TryComp<AbductorScientistComponent>(args.User, out var scientistComp))
            scientistComp.Console = ent.Owner;

        if (TryComp<AbductorAgentComponent>(args.User, out var agentComp))
            agentComp.Console = ent.Owner;

        var stations = _stationSystem.GetStations();
        var result = new Dictionary<int, StationBeacons>();

        foreach (var station in stations)
        {
            if (!TryComp(station, out MetaDataComponent? meta) || meta.EntityPrototype == null)
                continue;

            if (meta.EntityPrototype.ID != _nanoStation)
                continue;

            if (_stationSystem.GetLargestGrid(station) is not { } grid
                || !TryComp(station, out MetaDataComponent? stationMetaData))
                continue;

            if (!_entityManager.TryGetComponent<NavMapComponent>(grid, out var navMap))
                continue;

            result.Add(station.Id, new StationBeacons
            {
                Name = stationMetaData.EntityName,
                StationId = station.Id,
                Beacons = [.. navMap.Beacons.Values],
            });
        }

        _uiSystem.SetUiState(ent.Owner, AbductorCameraConsoleUIKey.Key, new AbductorCameraConsoleBuiState() { Stations = result });
    }

    private void SetEye(EntityUid uid, EntityUid eye, AbductorBeaconChosenBuiMsg args)
    {
        if (!TryComp(uid, out EyeComponent? eyeComp))
            return;

        AddComp(uid, new StationAiOverlayComponent { AllowCrossGrid = true });

        _eye.RefreshVisibilityMask(uid);
        _eye.SetTarget(uid, eye, eyeComp);
        _eye.SetDrawFov(uid, false);

        AddActions(args);
        _mover.SetRelay(uid, eye);
    }

    private void RemoveEye(EntityUid uid)
    {
        if (!TryComp(uid, out EyeComponent? eyeComp))
            return;

        RemComp<RelayInputMoverComponent>(uid);
        RemComp<StationAiOverlayComponent>(uid);

        _eye.SetTarget(uid, null);
        _eye.SetVisibilityMask(uid, eyeComp.VisibilityMask ^ (int)VisibilityFlags.Abductor, eyeComp);
        _eye.SetDrawFov(uid, true);

        RemoveActions(uid);
    }

    private void AddVirtualItems(EntityUid uid, EntityUid console)
    {
        if (!TryComp<HandsComponent>(uid, out var hands))
            return;

        foreach (var hand in _hands.EnumerateHands((uid, hands)))
        {
            var heldItem = _hands.GetHeldItem((uid, hands), hand);
            if (HasComp<UnremoveableComponent>(heldItem))
                continue;

            _hands.DoDrop(uid, hand);
        }

        if (_virtualItem.TrySpawnVirtualItemInHand(console, uid, out var virtItem1))
            EnsureComp<UnremoveableComponent>(virtItem1.Value);

        if (_virtualItem.TrySpawnVirtualItemInHand(console, uid, out var virtItem2))
            EnsureComp<UnremoveableComponent>(virtItem2.Value);
    }
}
