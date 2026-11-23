using Content.Server.Actions;
using Content.Shared.Actions.Components;
using Content.Shared._Sunrise.Silicons.StationAi;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.Silicons.StationAi;

public sealed partial class StationAiBodySystem
{
    /*
     * Interface partial.
     *
     * This file owns player-facing controls for station AI bodies:
     * action buttons, the alternative verb for entering a body, bound UI messages,
     * and the body list sent to the AI body selection interface.
     */

    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly SpriteSpecifier BodyEnterVerbIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/in.svg.192dpi.png"));

    #region Initialize

    /// <summary>
    /// Subscribes action, verb, and bound UI handlers for station AI body control.
    /// </summary>
    private void InitializeBodyInterface()
    {
        SubscribeLocalEvent<StationAiBodyControllerComponent, ComponentStartup>(OnStationAiBodyControllerStartup);
        SubscribeLocalEvent<StationAiBodyControllerComponent, MapInitEvent>(OnStationAiBodyControllerMapInit);
        SubscribeLocalEvent<StationAiBodyControllerComponent, ComponentShutdown>(OnStationAiBodyControllerShutdown);
        SubscribeLocalEvent<StationAiBodyControllerComponent, StationAiBodyOpenUiActionEvent>(OnStationAiOpenBodyUiAction);
        SubscribeLocalEvent<StationAiBodyOpenUiActionEvent>(OnOpenBodyUiAction);
        SubscribeLocalEvent<StationAiBodyComponent, GetVerbsEvent<AlternativeVerb>>(OnBodyAlternativeVerbs);
        SubscribeLocalEvent<StationAiBodyComponent, StationAiBodyOpenUiActionEvent>(OnBodyOpenBodyUiAction);

        Subs.BuiEvents<StationAiBodyControllerComponent>(StationAiBodyUiKey.Key,
            subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnBodyUiOpened);
            subs.Event<StationAiBodyEnterMessage>(OnBodyUiEnterMessage);
            subs.Event<StationAiBodyExitMessage>(OnBodyUiExitMessage);
        });
    }

    #endregion

    #region Events

    /// <summary>
    /// Ensures body control actions and UI data when the controller component starts.
    /// </summary>
    private void OnStationAiBodyControllerStartup(Entity<StationAiBodyControllerComponent> stationAi, ref ComponentStartup args)
    {
        EnsureControllerActions(stationAi);
        UpdateBodyUiData(stationAi.AsNullable());
    }

    /// <summary>
    /// Ensures body control actions and UI data when the controller is map-initialized.
    /// </summary>
    private void OnStationAiBodyControllerMapInit(Entity<StationAiBodyControllerComponent> stationAi, ref MapInitEvent args)
    {
        EnsureControllerActions(stationAi);
        UpdateBodyUiData(stationAi.AsNullable());
    }

    /// <summary>
    /// Removes the body menu action when the AI controller component shuts down.
    /// </summary>
    private void OnStationAiBodyControllerShutdown(Entity<StationAiBodyControllerComponent> stationAi, ref ComponentShutdown args)
    {
        _actions.RemoveAction((EntityUid) stationAi, stationAi.Comp.BodyMenuAction);
        stationAi.Comp.BodyMenuAction = null;
    }

    /// <summary>
    /// Opens the body selection UI from the station AI brain action.
    /// </summary>
    private void OnStationAiOpenBodyUiAction(EntityUid stationAi, StationAiBodyControllerComponent component, StationAiBodyOpenUiActionEvent args)
    {
        if (args.Handled)
            return;

        if (!TryOpenBodyUi(stationAi, args.Performer))
            return;

        args.Handled = true;
    }

    /// <summary>
    /// Opens the body selection UI from the currently controlled body action.
    /// </summary>
    private void OnBodyOpenBodyUiAction(EntityUid bodyUid, StationAiBodyComponent body, StationAiBodyOpenUiActionEvent args)
    {
        if (args.Handled || body.LinkedAi is not { } stationAi)
            return;

        if (!TryOpenBodyUi(stationAi, args.Performer))
            return;

        args.Handled = true;
    }

    /// <summary>
    /// Opens the body selection UI from a relayed station AI action.
    /// </summary>
    private void OnOpenBodyUiAction(StationAiBodyOpenUiActionEvent args)
    {
        if (args.Handled || !TryGetStationAiFromBodyUiAction(args, out var stationAi, out var actor))
            return;

        if (!TryOpenBodyUi(stationAi, actor))
            return;

        args.Handled = true;
    }

    /// <summary>
    /// Adds the alternative verb that lets a station AI enter a free prepared body.
    /// </summary>
    private void OnBodyAlternativeVerbs(Entity<StationAiBodyComponent> body, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanComplexInteract || !TryGetStationAiFromActor(args.User, out var stationAi))
            return;

        if (!CanEnterBody(stationAi, body.AsNullable(), out _, out _, out _))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("station-ai-body-enter-verb"),
            Icon = BodyEnterVerbIcon,
            Act = () => TryEnterBody(stationAi, body.AsNullable()),
        });
    }

    /// <summary>
    /// Refreshes body entries when the body selection UI is opened.
    /// </summary>
    private void OnBodyUiOpened(EntityUid stationAi, StationAiBodyControllerComponent controller, BoundUIOpenedEvent args)
    {
        UpdateBodyUiData((stationAi, controller));
    }

    /// <summary>
    /// Handles a UI request to transfer control into a selected body.
    /// </summary>
    private void OnBodyUiEnterMessage(EntityUid stationAi, StationAiBodyControllerComponent controller, StationAiBodyEnterMessage args)
    {
        if (!CanUseBodyUi((stationAi, controller), args.Actor))
            return;

        if (!TryGetEntity(args.Body, out var body))
            return;

        TryEnterBody(stationAi, body.Value);
    }

    /// <summary>
    /// Handles a UI request to return control from the body to the AI brain.
    /// </summary>
    private void OnBodyUiExitMessage(EntityUid stationAi, StationAiBodyControllerComponent controller, StationAiBodyExitMessage args)
    {
        if (!CanUseBodyUi((stationAi, controller), args.Actor))
            return;

        TryExitBody(stationAi);
    }

    #endregion

    #region Actions

    /// <summary>
    /// Grants actions that should be available only while an AI controls this body.
    /// </summary>
    private void GrantControlledBodyActions(Entity<StationAiBodyComponent> body)
    {
        RevokeControlledBodyActions(body);

        foreach (var action in body.Comp.ControlledBodyActions)
        {
            EntityUid? actionEnt = null;
            _actions.AddAction(body, ref actionEnt, action);

            if (actionEnt != null)
                body.Comp.ControlledBodyActionEntities.Add(actionEnt.Value);
        }
    }

    /// <summary>
    /// Removes actions that were granted while the AI controlled this body.
    /// </summary>
    private void RevokeControlledBodyActions(Entity<StationAiBodyComponent> body)
    {
        foreach (var actionEnt in body.Comp.ControlledBodyActionEntities)
        {
            _actions.RemoveAction((EntityUid) body, actionEnt);
        }

        body.Comp.ControlledBodyActionEntities.Clear();
    }

    /// <summary>
    /// Ensures the AI brain has the action used to open the body selection UI.
    /// </summary>
    private void EnsureControllerActions(Entity<StationAiBodyControllerComponent> stationAi)
    {
        _actions.AddAction(stationAi, ref stationAi.Comp.BodyMenuAction, stationAi.Comp.BodyMenuActionPrototype);
        Dirty(stationAi);
    }

    #endregion

    #region UI Data

    /// <summary>
    /// Rebuilds and networks body selection UI data for one station AI controller.
    /// </summary>
    private void UpdateBodyUiData(Entity<StationAiBodyControllerComponent?> stationAi)
    {
        if (!Resolve(stationAi, ref stationAi.Comp, false))
            return;

        stationAi.Comp.Bodies = BuildBodyEntries(GetCurrentBody(stationAi));
        Dirty(stationAi);
    }

    /// <summary>
    /// Rebuilds body selection UI data for every station AI controller.
    /// </summary>
    private void UpdateAllBodyUiData()
    {
        var query = EntityQueryEnumerator<StationAiBodyControllerComponent>();

        while (query.MoveNext(out var stationAi, out var controller))
        {
            UpdateBodyUiData((stationAi, controller));
        }
    }

    /// <summary>
    /// Builds the sorted list of prepared AI bodies sent to the body selection UI.
    /// </summary>
    private List<StationAiBodyEntry> BuildBodyEntries(EntityUid? currentBody)
    {
        var bodies = new List<StationAiBodyEntry>();
        var query = EntityQueryEnumerator<StationAiBodyComponent, MetaDataComponent>();

        while (query.MoveNext(out var bodyUid, out var body, out var meta))
        {
            if (body.Board == null)
                continue;

            NetEntity? linkedAi = body.LinkedAi is { } ai ? GetNetEntity(ai) : null;
            bodies.Add(new StationAiBodyEntry(
                GetNetEntity(bodyUid),
                body.BodyNumber,
                meta.EntityName,
                linkedAi,
                currentBody == bodyUid));
        }

        bodies.Sort((left, right) => left.BodyNumber.CompareTo(right.BodyNumber));
        return bodies;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Opens the body selection UI for a station AI actor after validating access.
    /// </summary>
    private bool TryOpenBodyUi(EntityUid stationAi, EntityUid actor)
    {
        if (!CanOpenBodyUi(stationAi, actor, out var controller))
            return false;

        UpdateBodyUiData((stationAi, controller));
        return _ui.TryOpenUi(stationAi, StationAiBodyUiKey.Key, GetBodyUiActor(stationAi, actor));
    }

    /// <summary>
    /// Returns whether the actor may open the body selection UI for this station AI.
    /// </summary>
    private bool CanOpenBodyUi(EntityUid stationAi, EntityUid actor, out StationAiBodyControllerComponent controller)
    {
        controller = default!;

        if (!TryComp(stationAi, out StationAiBodyControllerComponent? controllerComp))
            return false;

        if (!CanUseBodyUi((stationAi, controllerComp), actor))
            return false;

        controller = controllerComp;
        return true;
    }

    /// <summary>
    /// Returns whether the actor may use the body selection UI for this station AI.
    /// </summary>
    private bool CanUseBodyUi(Entity<StationAiBodyControllerComponent> stationAi, EntityUid actor)
    {
        return IsBodyUiActor(stationAi, actor);
    }

    /// <summary>
    /// Returns whether the actor is allowed to use the body selection UI for this station AI.
    /// </summary>
    private bool IsBodyUiActor(Entity<StationAiBodyControllerComponent> stationAi, EntityUid actor)
    {
        if (actor == (EntityUid) stationAi)
            return true;

        if (IsRemoteActor(stationAi, actor))
            return true;

        return stationAi.Comp.CurrentBody == actor &&
               TryComp<StationAiBodyComponent>(actor, out var body) &&
               body.LinkedAi == (EntityUid) stationAi;
    }

    /// <summary>
    /// Returns whether the actor is the remote AI eye for this station AI brain.
    /// </summary>
    private bool IsRemoteActor(EntityUid stationAi, EntityUid actor)
    {
        return _container.TryGetContainingContainer(stationAi, out var container) &&
               container.ID == StationAiCoreComponent.Container &&
               TryComp<StationAiCoreComponent>(container.Owner, out var core) &&
               core.RemoteEntity == actor;
    }

    /// <summary>
    /// Returns the entity that should be marked as the BUI actor for the player's current session.
    /// </summary>
    private EntityUid GetBodyUiActor(EntityUid stationAi, EntityUid actor)
    {
        var isActor = IsRemoteActor(stationAi, actor);
        return isActor ? stationAi : actor;
    }

    /// <summary>
    /// Resolves the station AI brain for body UI actions raised on the active actor or relayed action target.
    /// </summary>
    private bool TryGetStationAiFromBodyUiAction(StationAiBodyOpenUiActionEvent args, out EntityUid stationAi, out EntityUid actor)
    {
        actor = args.Performer;

        if (TryGetStationAiFromActor(args.Performer, out stationAi))
            return true;

        if (TryComp<ActionComponent>(args.Action, out var action) &&
            action.AttachedEntity is { } attached)
        {
            actor = attached;
            return TryGetStationAiFromActor(attached, out stationAi);
        }

        stationAi = default;
        actor = default;
        return false;
    }

    /// <summary>
    /// Resolves a station AI brain from either the brain itself or its currently controlled body.
    /// </summary>
    private bool TryGetStationAiFromActor(EntityUid actor, out EntityUid stationAi)
    {
        if (HasComp<StationAiBodyControllerComponent>(actor))
        {
            stationAi = actor;
            return true;
        }

        if (TryGetStationAiFromRemoteActor(actor, out stationAi))
            return true;

        if (!TryComp<StationAiBodyComponent>(actor, out var body))
        {
            stationAi = default;
            return false;
        }

        if (body.LinkedAi is not { } linkedAi)
        {
            stationAi = default;
            return false;
        }

        if (!HasComp<StationAiBodyControllerComponent>(linkedAi))
        {
            stationAi = default;
            return false;
        }

        stationAi = linkedAi;
        return true;
    }

    /// <summary>
    /// Resolves a station AI brain from its active remote eye.
    /// </summary>
    private bool TryGetStationAiFromRemoteActor(EntityUid actor, out EntityUid stationAi)
    {
        var query = EntityQueryEnumerator<StationAiCoreComponent>();

        while (query.MoveNext(out var coreUid, out var core))
        {
            if (core.RemoteEntity != actor)
                continue;

            if (!_container.TryGetContainer(coreUid, StationAiHolderComponent.Container, out var container) ||
                container.ContainedEntities.Count != 1)
                break;

            stationAi = container.ContainedEntities[0];
            return HasComp<StationAiBodyControllerComponent>(stationAi);
        }

        stationAi = default;
        return false;
    }

    #endregion
}
