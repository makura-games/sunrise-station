using System.Linq;
using Content.Server.Popups;
using Content.Shared._Starlight.Action;
using Content.Shared.Actions.Components;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.Inventory;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared._Sunrise.Antags.Abductor;
using Content.Shared._Sunrise.Movement.Carrying;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Physics.Events;
using Content.Shared.Popups;

namespace Content.Server._Sunrise.Antags.Abductor;

public sealed partial class AbductorSystem : SharedAbductorSystem
{
    [Dependency] private SharedAudioSystem _audioSystem = default!;
    [Dependency] private SharedColorFlashEffectSystem _color = default!;
    [Dependency] private PullingSystem _pullingSystem = default!;
    [Dependency] private InventorySystem _inv = default!;
    [Dependency] private StarlightActionsSystem _starlightActions = default!;
    [Dependency] private PopupSystem _popupSystem = default!;

    private static readonly EntProtoId<InstantActionComponent> _gizmoMark = "ActionGizmoMark";
    private static readonly EntProtoId<InstantActionComponent> _sendAgent = "ActionSendAgent";
    private static readonly EntProtoId<InstantActionComponent> _sendYourself = "ActionSendYourself";
    private static readonly EntProtoId<InstantActionComponent> _exitAction = "ActionExitConsole";

    private static readonly EntProtoId _teleportationEffect = "EffectTeleportation";
    private static readonly EntProtoId _teleportationEffectEntity = "EffectTeleportationEntity";
    public void InitializeActions()
    {
        SubscribeLocalEvent<AbductorScientistComponent, ComponentStartup>(AbductorScientistComponentStartup);
        SubscribeLocalEvent<AbductorAgentComponent, ComponentStartup>(AbductorAgentComponentStartup);

        SubscribeLocalEvent<ExitConsoleEvent>(OnExit);

        SubscribeLocalEvent<AbductorReturnToShipEvent>(OnReturn);
        SubscribeLocalEvent<AbductorScientistComponent, AbductorReturnDoAfterEvent>(OnDoAfterAbductorScientistReturn);
        SubscribeLocalEvent<AbductorAgentComponent, AbductorReturnDoAfterEvent>(OnDoAfterAbductorAgentReturn);
        SubscribeLocalEvent<AbductorAlienPadComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<AbductorAlienPadComponent, EndCollideEvent>(OnEndCollide);
        SubscribeLocalEvent<SendYourselfEvent>(OnSendYourself);
        SubscribeLocalEvent<SendAgentEvent>(OnSendAgent);
        SubscribeLocalEvent<AbductorScientistComponent, AbductorSendYourselfDoAfterEvent>(OnDoAfterAbductorScientistSendYourself);
        SubscribeLocalEvent<AbductorAgentComponent, AbductorSendYourselfDoAfterEvent>(OnDoAfterAbductorAgentSendYourself);

        SubscribeLocalEvent<GizmoMarkEvent>(OnGizmoMark);
    }
    private void OnStartCollide(Entity<AbductorAlienPadComponent> ent, ref StartCollideEvent args)
    {
        if (!HasComp<AbductorAgentComponent>(args.OtherEntity))
            return;

        EnsureComp<AbductorOnAlienPadComponent>(args.OtherEntity);
    }
    private void OnEndCollide(Entity<AbductorAlienPadComponent> ent, ref EndCollideEvent args)
    {
        if (!HasComp<AbductorAgentComponent>(args.OtherEntity))
            return;

        RemComp<AbductorOnAlienPadComponent>(args.OtherEntity);
    }
    private void AbductorScientistComponentStartup(Entity<AbductorScientistComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.SpawnPosition = EnsureComp<TransformComponent>(ent).Coordinates;

        EnsureComp<TransformComponent>(ent, out var xform);
        var console = _entityLookup.GetEntitiesInRange<AbductorConsoleComponent>(xform.Coordinates, 4, LookupFlags.Approximate | LookupFlags.Dynamic).FirstOrDefault();

        if (console == default)
            return;

        console.Comp.Scientist = ent;
        SyncAbductors(console);
    }

    private void AbductorAgentComponentStartup(Entity<AbductorAgentComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.SpawnPosition = EnsureComp<TransformComponent>(ent).Coordinates;

        EnsureComp<TransformComponent>(ent, out var xform);
        var console = _entityLookup.GetEntitiesInRange<AbductorConsoleComponent>(xform.Coordinates, 4, LookupFlags.Approximate | LookupFlags.Dynamic).FirstOrDefault();

        if (console == default)
            return;

        console.Comp.Agent = ent;
        SyncAbductors(console);
    }

    private void OnReturn(AbductorReturnToShipEvent ev)
    {
        AbductorAgentComponent? agentComp = null;
        if (!TryComp<AbductorScientistComponent>(ev.Performer, out var scientistComp) && !TryComp<AbductorAgentComponent>(ev.Performer, out agentComp))
            EnsureComp<AbductorScientistComponent>(ev.Performer, out scientistComp);

        if (HasComp<ActiveCarrierComponent>(ev.Performer))
        {
            _popupSystem.PopupEntity(Loc.GetString("need-stop-carry"), ev.Performer, ev.Performer, PopupType.MediumCaution);
            return;
        }

        EntityCoordinates? spawnPosition = null;

        if (scientistComp != null && scientistComp.SpawnPosition.HasValue)
            spawnPosition = scientistComp.SpawnPosition.Value;
        else if (agentComp != null && agentComp.SpawnPosition.HasValue)
            spawnPosition = agentComp.SpawnPosition.Value;

        if (spawnPosition == null)
            return;

        _color.RaiseEffect(Color.FromHex("#BA0099"), new List<EntityUid>(1) { ev.Performer }, Filter.Pvs(ev.Performer, entityManager: EntityManager));
        EnsureComp<TransformComponent>(ev.Performer, out var xform);
        var effectEnt = SpawnAttachedTo(_teleportationEffectEntity, xform.Coordinates);
        _xformSys.SetParent(effectEnt, ev.Performer);
        EnsureComp<TimedDespawnComponent>(effectEnt, out var despawnEffectEntComp);
        despawnEffectEntComp.Lifetime = 3.0f;
        _audioSystem.PlayPvs("/Audio/_Sunrise/Abductor/alien_teleport.ogg", effectEnt);


        var effect = _entityManager.SpawnEntity(_teleportationEffect, spawnPosition.Value);
        EnsureComp<TimedDespawnComponent>(effect, out var despawnComp);
        despawnComp.Lifetime = 3.0f;
        _audioSystem.PlayPvs("/Audio/_Sunrise/Abductor/alien_teleport.ogg", effect);

        var doAfter = new DoAfterArgs(EntityManager, ev.Performer, TimeSpan.FromSeconds(3), new AbductorReturnDoAfterEvent(), ev.Performer)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
        ev.Handled = true;
    }

    private void OnDoAfterAbductorScientistReturn(Entity<AbductorScientistComponent> ent, ref AbductorReturnDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        Return(ent, ent.Comp, null);
    }

    private void OnDoAfterAbductorAgentReturn(Entity<AbductorAgentComponent> ent, ref AbductorReturnDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        Return(ent, null, ent.Comp);
    }

    private void Return(EntityUid uid, AbductorScientistComponent? scientistComp, AbductorAgentComponent? agentComp)
    {

        _color.RaiseEffect(Color.FromHex("#BA0099"), new List<EntityUid>(1) { uid }, Filter.Pvs(uid, entityManager: EntityManager));
        if (_pullingSystem.IsPulling(uid))
        {
            if (!TryComp<PullerComponent>(uid, out var pullerComp)
                || pullerComp.Pulling == null
                || !TryComp<PullableComponent>(pullerComp.Pulling.Value, out var pullableComp)
                || !_pullingSystem.TryStopPull(pullerComp.Pulling.Value, pullableComp)) return;
        }

        if (_pullingSystem.IsPulled(uid))
        {
            if (!TryComp<PullableComponent>(uid, out var pullableComp)
                || !_pullingSystem.TryStopPull(uid, pullableComp)) return;
        }

        EntityCoordinates? spawnPosition = null;

        if (scientistComp != null && scientistComp.SpawnPosition.HasValue)
            spawnPosition = scientistComp.SpawnPosition.Value;
        else if (agentComp != null && agentComp.SpawnPosition.HasValue)
            spawnPosition = agentComp.SpawnPosition.Value;

        if (spawnPosition == null)
            return;

        _xformSys.SetCoordinates(uid, spawnPosition.Value);
        OnCameraExit(uid);
    }

    private void OnSendYourself(SendYourselfEvent ev)
    {
        _color.RaiseEffect(Color.FromHex("#BA0099"), new List<EntityUid>(1) { ev.Performer }, Filter.Pvs(ev.Performer, entityManager: EntityManager));

        EnsureComp<TransformComponent>(ev.Performer, out var xform);
        var effectEnt = SpawnAttachedTo(_teleportationEffectEntity, xform.Coordinates);
        _xformSys.SetParent(effectEnt, ev.Performer);
        EnsureComp<TimedDespawnComponent>(effectEnt, out var despawnEffectEntComp);

        var effect = _entityManager.SpawnEntity(_teleportationEffect, ev.Target);
        EnsureComp<TimedDespawnComponent>(effect, out var despawnComp);

        var @event = new AbductorSendYourselfDoAfterEvent(GetNetCoordinates(ev.Target));

        var doAfter = new DoAfterArgs(EntityManager, ev.Performer, TimeSpan.FromSeconds(5), @event, ev.Performer);
        _doAfter.TryStartDoAfter(doAfter);

        ev.Handled = true;
    }

    private void OnSendAgent(SendAgentEvent ev)
    {
        bool foundAny = false;
        var query = EntityQueryEnumerator<AbductorOnAlienPadComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!TryComp(ev.Performer, out TransformComponent? perXform))
                continue;

            if (perXform.GridUid != xform.GridUid)
                continue;

            _color.RaiseEffect(Color.FromHex("#BA0099"), new List<EntityUid>(1) { uid }, Filter.Pvs(uid, entityManager: EntityManager));

            var effectEnt = SpawnAttachedTo(_teleportationEffectEntity, xform.Coordinates);
            _xformSys.SetParent(effectEnt, uid);
            EnsureComp<TimedDespawnComponent>(effectEnt, out var despawnEffectEntComp);

            var effect = _entityManager.SpawnEntity(_teleportationEffect, ev.Target);
            EnsureComp<TimedDespawnComponent>(effect, out var despawnComp);

            var @event = new AbductorSendYourselfDoAfterEvent(GetNetCoordinates(ev.Target)); // не знаю пригодится ли тут дуафтер, нужно тестить с несколькими клиентами
            var doAfter = new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(5), @event, uid);

            foundAny = true;

            _doAfter.TryStartDoAfter(doAfter);
            ev.Handled = true;

            RemComp<AbductorOnAlienPadComponent>(uid);
        }
        if (!foundAny)
        {
            _popupSystem.PopupCursor(Loc.GetString("alian-pad-is-empty"), ev.Performer);
        }
    }

    private void OnDoAfterAbductorScientistSendYourself(Entity<AbductorScientistComponent> ent, ref AbductorSendYourselfDoAfterEvent args)
    {
        OnDoAfterSendYourself(ent, args);
    }

    private void OnDoAfterAbductorAgentSendYourself(Entity<AbductorAgentComponent> ent, ref AbductorSendYourselfDoAfterEvent args)
    {
        OnDoAfterSendYourself(ent, args);
    }

    private void OnDoAfterSendYourself(EntityUid ent, AbductorSendYourselfDoAfterEvent args)
    {
        _color.RaiseEffect(Color.FromHex("#BA0099"), new List<EntityUid>(1) { ent }, Filter.Pvs(ent, entityManager: EntityManager));
        if (_pullingSystem.IsPulling(ent))
        {
            if (!TryComp<PullerComponent>(ent, out var pullerComp)
                || pullerComp.Pulling == null
                || !TryComp<PullableComponent>(pullerComp.Pulling.Value, out var pullableComp)
                || !_pullingSystem.TryStopPull(pullerComp.Pulling.Value, pullableComp)) return;
        }

        if (_pullingSystem.IsPulled(ent))
        {
            if (!TryComp<PullableComponent>(ent, out var pullableComp)
                || !_pullingSystem.TryStopPull(ent, pullableComp)) return;
        }
        _xformSys.SetCoordinates(ent, GetCoordinates(args.TargetCoordinates));
        OnCameraExit(ent);
    }

    private void OnGizmoMark(GizmoMarkEvent ev)
    {
        if (!HasComp<AbductorComponent>(ev.Target))
        {
            _popupSystem.PopupCursor(Loc.GetString("target-is-not-abductor"), ev.Performer);
            return;
        }
        if (!_inv.TryGetSlotContainer(ev.Performer, "pocket1", out var pocket1, out _) ||
            !_inv.TryGetSlotContainer(ev.Performer, "pocket2", out var pocket2, out _))
        {
            _popupSystem.PopupCursor(Loc.GetString("no-gizmo-in-pockets"), ev.Performer);
            return;
        }
        var pocket1PossibleGizmo = pocket1.ContainedEntity;
        var pocket2PossibleGizmo = pocket2.ContainedEntity;

        if (TryComp<AbductorGizmoComponent>(pocket1PossibleGizmo, out var pocket1Gizmo))
        {
            pocket1Gizmo.Target = GetNetEntity(ev.Target);
            _popupSystem.PopupCursor(Loc.GetString("gizmo-is-successfully-filled"), ev.Performer);
            return;
        }
        if (TryComp<AbductorGizmoComponent>(pocket2PossibleGizmo, out var pocket2Gizmo))
        {
            pocket2Gizmo.Target = GetNetEntity(ev.Target);
            _popupSystem.PopupCursor(Loc.GetString("gizmo-is-successfully-filled"), ev.Performer);
            return;
        }
        _popupSystem.PopupCursor(Loc.GetString("gizmo-is-not-in-pockets"), ev.Performer);
    }

    private void OnExit(ExitConsoleEvent ev) => OnCameraExit(ev.Performer);

    private void AddActions(AbductorBeaconChosenBuiMsg args)
    {
        EnsureComp<AbductorsAbilitiesComponent>(args.Actor, out var comp);
        comp.HiddenActions = _starlightActions.HideActions(args.Actor);
        _actions.AddAction(args.Actor, ref comp.ExitConsole, _exitAction);
        _actions.AddAction(args.Actor, ref comp.SendYourself, _sendYourself);
        _actions.AddAction(args.Actor, ref comp.GizmoMark, _gizmoMark);
        _actions.AddAction(args.Actor, ref comp.SendAgent, _sendAgent);
    }
    private void RemoveActions(EntityUid actor)
    {
        EnsureComp<AbductorsAbilitiesComponent>(actor, out var comp);

        if (comp.ExitConsole is not null)
            _actions.RemoveAction(actor, comp.ExitConsole);

        if (comp.SendYourself is not null)
            _actions.RemoveAction(actor, comp.SendYourself);

        if (comp.SendAgent is not null)
            _actions.RemoveAction(actor, comp.SendAgent);

        if (comp.GizmoMark is not null)
            _actions.RemoveAction(actor, comp.GizmoMark);

        _starlightActions.UnHideActions(actor, comp.HiddenActions);
    }
}
