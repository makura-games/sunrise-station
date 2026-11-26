using Content.Server.Mind;
using Content.Shared._Sunrise.Silicons.StationAi;
using Content.Shared._Sunrise.TTS;
using Content.Shared.Access.Systems;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.NameIdentifier;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Silicons.StationAi;

public sealed partial class StationAiBodySystem
{
    /*
     * State partial.
     *
     * This file owns the gameplay state machine of station AI bodies:
     * preparing a borg chassis as a free AI body, entering and leaving a body,
     * recovering the AI during body deletion or board removal, and transferring MindComponent
     * between the AI brain entity and the controlled body.
     */

    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedAccessSystem _access = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> StationAiCommunicationBoardTag = "StationAiCommunicationBoard";

    #region Initialize

    /// <summary>
    /// Subscribes state transition handlers for AI bodies and their controlling AI brains.
    /// </summary>
    private void InitializeBodyState()
    {
        SubscribeLocalEvent<BorgChassisComponent, BorgBrainInsertedIntoChassisEvent>(OnBorgBrainInserted);
        SubscribeLocalEvent<BorgChassisComponent, BorgBrainRemovedFromChassisEvent>(OnBorgBrainRemoved);
        SubscribeLocalEvent<StationAiBodyControllerComponent, EntGotRemovedFromContainerMessage>(OnStationAiBrainGotRemovedFromCore);
        SubscribeLocalEvent<StationAiBodyControllerComponent, MobStateChangedEvent>(OnStationAiBrainMobStateChanged);
        SubscribeLocalEvent<StationAiBodyComponent, EntityTerminatingEvent>(OnBodyTerminating);
        SubscribeLocalEvent<StationAiBodyComponent, StationAiBodyExitActionEvent>(OnBodyExitAction);
    }

    #endregion

    #region Events

    /// <summary>
    /// Attempts to initialize a borg chassis as an AI body when a communication board enters its brain slot.
    /// </summary>
    private void OnBorgBrainInserted(Entity<BorgChassisComponent> chassis, ref BorgBrainInsertedIntoChassisEvent args)
    {
        TryInitializeBody(chassis.AsNullable(), args.Brain);
    }

    /// <summary>
    /// Clears AI body state when its communication board leaves the borg brain slot.
    /// </summary>
    private void OnBorgBrainRemoved(Entity<BorgChassisComponent> chassis, ref BorgBrainRemovedFromChassisEvent args)
    {
        TryClearBodyFromRemovedBoard(chassis.AsNullable(), args.Brain);
    }

    /// <summary>
    /// Returns the station AI to its brain if the controlled body is terminating.
    /// </summary>
    private void OnBodyTerminating(Entity<StationAiBodyComponent> body, ref EntityTerminatingEvent args)
    {
        TryEmergencyReturnFromBody(body.AsNullable(), ejectBoard: true);
    }

    /// <summary>
    /// Forces the AI out of a controlled body when its brain is removed from the core.
    /// </summary>
    private void OnStationAiBrainGotRemovedFromCore(Entity<StationAiBodyControllerComponent> stationAi, ref EntGotRemovedFromContainerMessage args)
    {
        if (args.Container.ID != StationAiCoreComponent.Container)
            return;

        if (!HasComp<StationAiCoreComponent>(args.Container.Owner))
            return;

        TryExitBodyFromCore(stationAi);
    }

    /// <summary>
    /// Forces the AI out of a controlled body when the AI brain dies.
    /// </summary>
    private void OnStationAiBrainMobStateChanged(Entity<StationAiBodyControllerComponent> stationAi, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive)
            return;

        TryExitBody(stationAi);
    }

    /// <summary>
    /// Handles the body exit action raised from a currently controlled body.
    /// </summary>
    private void OnBodyExitAction(EntityUid bodyUid, StationAiBodyComponent body, StationAiBodyExitActionEvent args)
    {
        if (args.Handled || body.LinkedAi is not { } stationAi)
            return;

        if (!TryExitBody(stationAi))
            return;

        args.Handled = true;
    }

    #endregion

    #region Public API

    /// <summary>
    /// Prepares a borg chassis as a free station AI body if the inserted entity is an AI communication board.
    /// </summary>
    public bool TryInitializeBody(Entity<BorgChassisComponent?> chassis, EntityUid board)
    {
        if (!CanInitializeBody(chassis, board))
            return false;

        DoInitializeBody(chassis, board);
        return true;
    }

    /// <summary>
    /// Returns whether the chassis can become a station AI body from the supplied board.
    /// </summary>
    public bool CanInitializeBody(Entity<BorgChassisComponent?> chassis, EntityUid board)
    {
        return Resolve(chassis, ref chassis.Comp, false) &&
               _tag.HasTag(board, StationAiCommunicationBoardTag);
    }

    /// <summary>
    /// Transfers a station AI brain into a prepared free body.
    /// </summary>
    public bool TryEnterBody(EntityUid stationAi, Entity<StationAiBodyComponent?> body)
    {
        if (!CanEnterBody(stationAi, body, out var mindId, out var mind, out var currentBody))
            return false;

        if (!Resolve(body, ref body.Comp, false))
            return false;

        DoEnterBody(stationAi, (body, body.Comp!), mindId, mind, currentBody);
        return true;
    }

    /// <summary>
    /// Returns whether the AI may start controlling the supplied body.
    /// </summary>
    public bool CanEnterBody(
        EntityUid stationAi,
        Entity<StationAiBodyComponent?> body,
        out EntityUid mindId,
        out MindComponent mind,
        out EntityUid? currentBody)
    {
        mindId = default;
        mind = default!;
        currentBody = null;

        if (!Resolve(body, ref body.Comp, false))
            return false;

        if (body.Comp.Board == null)
            return false;

        if (body.Comp.LinkedAi != null && body.Comp.LinkedAi != stationAi)
            return false;

        if (!_mind.TryGetMind(stationAi, out mindId, out var mindComp))
        {
            if (!TryGetCurrentControlledBody(stationAi, out var controlledBody, out _))
                return false;

            if (!_mind.TryGetMind(controlledBody, out mindId, out mindComp))
                return false;

            currentBody = controlledBody;
        }
        else if (TryComp<StationAiBodyControllerComponent>(stationAi, out var controller))
        {
            currentBody = controller.CurrentBody;
        }

        mind = mindComp;
        return true;
    }

    /// <summary>
    /// Returns a controlling AI brain from its current body back to the AI brain entity.
    /// </summary>
    public bool TryExitBody(EntityUid stationAi)
    {
        if (!CanExitBody(stationAi, out var body, out var bodyComp, out var mindId, out var mind, out var controller))
            return false;

        DoExitBody(stationAi, (body, bodyComp), mindId, mind, controller);
        return true;
    }

    /// <summary>
    /// Returns the AI to its brain after the brain was removed from the station AI core.
    /// </summary>
    public bool TryExitBodyFromCore(Entity<StationAiBodyControllerComponent> stationAi)
    {
        if (!CanExitBody(stationAi, out var body, out var bodyComp, out var mindId, out var mind, out var controller))
            return false;

        DoExitBodyFromCore(stationAi, (body, bodyComp), mindId, mind, controller);
        return true;
    }

    /// <summary>
    /// Returns whether the AI is currently controlling a body and can leave it.
    /// </summary>
    public bool CanExitBody(
        EntityUid stationAi,
        out EntityUid body,
        out StationAiBodyComponent bodyComp,
        out EntityUid mindId,
        out MindComponent mind,
        out StationAiBodyControllerComponent controller)
    {
        body = default;
        bodyComp = default!;
        mindId = default;
        mind = default!;
        controller = default!;

        if (!TryGetCurrentControlledBody(stationAi, out var currentBody, out var controllerComp))
            return false;

        if (!_mind.TryGetMind(currentBody, out mindId, out var mindComp))
            return false;

        body = currentBody;
        bodyComp = currentBody.Comp;
        mind = mindComp;
        controller = controllerComp;
        return true;
    }

    /// <summary>
    /// Clears AI-body state from a borg chassis after its communication board is removed.
    /// </summary>
    public bool TryClearBodyFromRemovedBoard(Entity<BorgChassisComponent?> chassis, EntityUid board)
    {
        if (!CanClearBodyFromRemovedBoard(
                chassis,
                board,
                out var body,
                out var stationAi,
                out var mindId,
                out var mind,
                out var controller))
        {
            return false;
        }

        DoClearBodyFromRemovedBoard(
            (chassis, body),
            stationAi,
            mindId,
            mind,
            controller);
        return true;
    }

    /// <summary>
    /// Returns whether the removed board belongs to a prepared AI body and can clear that state.
    /// </summary>
    public bool CanClearBodyFromRemovedBoard(
        Entity<BorgChassisComponent?> chassis,
        EntityUid board,
        out StationAiBodyComponent body,
        out EntityUid? stationAi,
        out EntityUid? mindId,
        out MindComponent? mind,
        out StationAiBodyControllerComponent? controller)
    {
        body = default!;
        stationAi = null;
        mindId = null;
        mind = null;
        controller = null;

        if (!Resolve(chassis, ref chassis.Comp, false))
            return false;

        if (!TryComp<StationAiBodyComponent>(chassis, out var bodyComp))
            return false;

        if (bodyComp.Board != board)
            return false;

        body = bodyComp;

        if (bodyComp.LinkedAi is not { } linkedAi)
            return true;

        if (!TryComp<StationAiBodyControllerComponent>(linkedAi, out var controllerComp))
            return false;

        if (!_mind.TryGetMind(chassis, out var foundMindId, out var foundMind))
            return false;

        stationAi = linkedAi;
        mindId = foundMindId;
        mind = foundMind;
        controller = controllerComp;
        return true;
    }

    /// <summary>
    /// Returns the AI from a body that is being forcibly removed.
    /// </summary>
    public bool TryEmergencyReturnFromBody(Entity<StationAiBodyComponent?> body, bool ejectBoard)
    {
        if (!CanEmergencyReturnFromBody(body, out var stationAi, out var mindId, out var mind, out var controller))
            return false;

        DoEmergencyReturnFromBody(
            (body, body.Comp!),
            stationAi,
            mindId,
            mind,
            controller,
            ejectBoard);

        return true;
    }

    /// <summary>
    /// Returns whether a controlled body can safely hand its mind back to the AI brain.
    /// </summary>
    public bool CanEmergencyReturnFromBody(
        Entity<StationAiBodyComponent?> body,
        out EntityUid stationAi,
        out EntityUid mindId,
        out MindComponent mind,
        out StationAiBodyControllerComponent controller)
    {
        stationAi = default;
        mindId = default;
        mind = default!;
        controller = default!;

        if (!Resolve(body, ref body.Comp, false))
            return false;

        if (body.Comp.LinkedAi is not { } linkedAi)
            return false;

        if (TerminatingOrDeleted(linkedAi))
            return false;

        if (!_mind.TryGetMind(body, out mindId, out var mindComp))
            return false;

        if (!TryComp<StationAiBodyControllerComponent>(linkedAi, out var controllerComp))
            return false;

        stationAi = linkedAi;
        mind = mindComp;
        controller = controllerComp;
        return true;
    }

    #endregion

    #region State Changes

    /// <summary>
    /// Removes body state and returns the AI mind after the body's communication board was removed.
    /// </summary>
    private void DoClearBodyFromRemovedBoard(
        Entity<StationAiBodyComponent> body,
        EntityUid? stationAi,
        EntityUid? mindId,
        MindComponent? mind,
        StationAiBodyControllerComponent? controller)
    {
        body.Comp.Board = null;
        body.Comp.LinkedAi = null;

        TryReturnMindFromRemovedBoard(stationAi, mindId, mind, controller);

        if (stationAi != null)
            RestoreBodyVoice(body);
        RemCompDeferred<IgnoreUIRangeComponent>(body);
        RevokeControlledBodyActions(body);
        RevokeStationAiRadioChannels(body);
        _siliconVoice.SetVoiceChangeEnabled(body, true);

        RemoveBodyStatusIcon(body);
        RemCompDeferred<StationAiBodyComponent>(body);
        UpdateAllBodyUiData();
    }

    /// <summary>
    /// Converts a borg chassis with an inserted communication board into an available AI body.
    /// </summary>
    private void DoInitializeBody(EntityUid chassis, EntityUid board)
    {
        var body = EnsureComp<StationAiBodyComponent>(chassis);

        if (body.BodyNumber == 0)
            body.BodyNumber = GetNextBodyNumber();

        body.Board = board;
        body.LinkedAi = null;
        _siliconVoice.SetVoiceChangeEnabled(chassis, false);

        RemComp<NameIdentifierComponent>(chassis);

        _metaData.SetEntityName(chassis, GetFreeBodyName(body.BodyNumber));

        Dirty(chassis, body);
        UpdateAllBodyUiData();
    }

    /// <summary>
    /// Transfers an AI mind into the selected body and applies active-control state.
    /// </summary>
    private void DoEnterBody(
        EntityUid stationAi,
        Entity<StationAiBodyComponent> body,
        EntityUid mindId,
        MindComponent mind,
        EntityUid? currentBody)
    {
        var controller = EnsureComp<StationAiBodyControllerComponent>(stationAi);
        var aiName = MetaData(stationAi).EntityName;

        ReleaseCurrentBody(stationAi, currentBody, body);

        body.Comp.LinkedAi = stationAi;
        EnsureBodyStatusIcon(body);
        EnsureComp<IgnoreUIRangeComponent>(body);
        _siliconVoice.SetVoiceChangeEnabled(body, false);
        CopyStationAiVoiceToBody(stationAi, body);
        controller.CurrentBody = body;
        EnsureControllerActions((stationAi, controller));

        _mind.TransferTo(mindId, body, mind: mind);
        RestoreFreeBodyAccess(currentBody);
        _metaData.SetEntityName(body, aiName);
        GrantStationAiRadioChannels(stationAi, body);
        GrantControlledBodyActions(body);

        Dirty(body);
        Dirty(stationAi, controller);
        UpdateAllBodyUiData();
    }

    /// <summary>
    /// Transfers an AI mind from the controlled body back to the AI brain.
    /// </summary>
    private void DoExitBody(
        EntityUid stationAi,
        Entity<StationAiBodyComponent> body,
        EntityUid mindId,
        MindComponent mind,
        StationAiBodyControllerComponent controller)
    {
        _ui.CloseUi(stationAi, StationAiBodyUiKey.Key);
        DoReleaseBody(body, stationAi);
        controller.CurrentBody = null;

        _mind.TransferTo(mindId, stationAi, mind: mind);
        RestoreFreeBodyAccess(body.Owner);

        Dirty(stationAi, controller);
        UpdateAllBodyUiData();
    }

    /// <summary>
    /// Applies body exit state after the station AI brain leaves the core.
    /// </summary>
    private void DoExitBodyFromCore(
        Entity<StationAiBodyControllerComponent> stationAi,
        Entity<StationAiBodyComponent> body,
        EntityUid mindId,
        MindComponent mind,
        StationAiBodyControllerComponent controller)
    {
        RemComp<RelayInputMoverComponent>(stationAi);

        if (TryComp<InputMoverComponent>(stationAi, out var mover))
        {
            mover.CanMove = false;
            Dirty(stationAi, mover);
        }

        DoExitBody(stationAi, body, mindId, mind, controller);
    }

    /// <summary>
    /// Returns an AI mind from a body during forced cleanup such as body deletion.
    /// </summary>
    private void DoEmergencyReturnFromBody(
        Entity<StationAiBodyComponent> body,
        EntityUid stationAi,
        EntityUid mindId,
        MindComponent mind,
        StationAiBodyControllerComponent controller,
        bool ejectBoard)
    {
        if (ejectBoard)
            TryEjectBodyBoard(body);

        body.Comp.LinkedAi = null;
        controller.CurrentBody = null;
        RestoreBodyVoice(body);
        RemCompDeferred<IgnoreUIRangeComponent>(body);
        RemoveBodyStatusIcon(body);
        RevokeControlledBodyActions(body);
        RevokeStationAiRadioChannels(body);

        _mind.TransferTo(mindId, stationAi, mind: mind);

        Dirty(body);
        Dirty(stationAi, controller);
        UpdateAllBodyUiData();
    }

    /// <summary>
    /// Releases a body from the supplied AI without moving the AI mind.
    /// </summary>
    private void DoReleaseBody(Entity<StationAiBodyComponent> body, EntityUid stationAi)
    {
        if (body.Comp.LinkedAi != stationAi)
            return;

        body.Comp.LinkedAi = null;
        RestoreBodyVoice(body);
        RemCompDeferred<IgnoreUIRangeComponent>(body);
        RemoveBodyStatusIcon(body);
        RevokeControlledBodyActions(body);
        RevokeStationAiRadioChannels(body);
        _metaData.SetEntityName(body, GetFreeBodyName(body.Comp.BodyNumber));
        RestoreFreeBodyAccess(body.Owner);

        Dirty(body);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Returns the body currently controlled by the station AI, if any.
    /// </summary>
    private EntityUid? GetCurrentBody(EntityUid stationAi)
    {
        if (!TryGetCurrentControlledBody(stationAi, out var body, out _))
            return null;

        return body;
    }

    private void RestoreFreeBodyAccess(EntityUid? body)
    {
        if (body is not { } bodyUid)
            return;

        if (!TryComp<StationAiBodyComponent>(bodyUid, out var bodyComp))
            return;

        if (bodyComp.Board == null)
            return;

        _access.SetAccessEnabled(bodyUid, true);
    }

    /// <summary>
    /// Copies the selected station AI voice to the body for the duration of active control.
    /// </summary>
    private void CopyStationAiVoiceToBody(EntityUid stationAi, Entity<StationAiBodyComponent> body)
    {
        if (!TryComp<BorgVoiceComponent>(body, out var bodyVoice))
            return;

        body.Comp.CachedBodyVoiceId ??= bodyVoice.SelectedVoiceId;

        if (TryComp<BorgVoiceComponent>(stationAi, out var stationAiVoice))
            bodyVoice.SelectedVoiceId = stationAiVoice.SelectedVoiceId;

        Dirty(body.Owner, bodyVoice);
    }

    /// <summary>
    /// Restores the body voice that was present before active AI control.
    /// </summary>
    private void RestoreBodyVoice(Entity<StationAiBodyComponent> body)
    {
        if (body.Comp.CachedBodyVoiceId is not { } cachedVoice)
            return;

        if (TryComp<BorgVoiceComponent>(body, out var bodyVoice))
        {
            bodyVoice.SelectedVoiceId = cachedVoice;
            Dirty(body.Owner, bodyVoice);
        }

        body.Comp.CachedBodyVoiceId = null;
    }

    private void EnsureBodyStatusIcon(Entity<StationAiBodyComponent> body)
    {
        if (HasComp<StatusIconComponent>(body))
            return;

        EnsureComp<StatusIconComponent>(body);
        body.Comp.AddedStatusIconComponent = true;
    }

    private void RemoveBodyStatusIcon(Entity<StationAiBodyComponent> body)
    {
        if (!body.Comp.AddedStatusIconComponent)
            return;

        RemCompDeferred<StatusIconComponent>(body);
        body.Comp.AddedStatusIconComponent = false;
    }

    /// <summary>
    /// Resolves the current controlled body and controller for a station AI brain.
    /// </summary>
    private bool TryGetCurrentControlledBody(
        EntityUid stationAi,
        out Entity<StationAiBodyComponent> body,
        out StationAiBodyControllerComponent controller)
    {
        body = default;
        controller = default!;

        if (!TryComp<StationAiBodyControllerComponent>(stationAi, out var controllerComp))
            return false;

        if (controllerComp.CurrentBody is not { } currentBody)
            return false;

        if (!TryComp<StationAiBodyComponent>(currentBody, out var bodyComp))
            return false;

        if (bodyComp.LinkedAi != stationAi)
            return false;

        controller = controllerComp;
        body = (currentBody, bodyComp);
        return true;
    }

    /// <summary>
    /// Removes the communication board from a body when the body is being forcibly cleaned up.
    /// </summary>
    private bool TryEjectBodyBoard(Entity<StationAiBodyComponent> body)
    {
        if (body.Comp.Board is not { } board)
            return false;

        if (!Exists(board))
            return false;

        if (TerminatingOrDeleted(board))
            return false;

        return _container.TryRemoveFromContainer(board, force: true);
    }

    /// <summary>
    /// Transfers a mind back to the AI brain when board removal interrupts body control.
    /// </summary>
    private bool TryReturnMindFromRemovedBoard(
        EntityUid? stationAi,
        EntityUid? mindId,
        MindComponent? mind,
        StationAiBodyControllerComponent? controller)
    {
        if (stationAi is not { } ai)
            return false;

        if (mindId is not { } aiMindId)
            return false;

        if (mind == null)
            return false;

        if (controller == null)
            return false;

        controller.CurrentBody = null;
        _mind.TransferTo(aiMindId, ai, mind: mind);
        Dirty(ai, controller);
        return true;
    }

    /// <summary>
    /// Releases the AI's previously controlled body before entering another one.
    /// </summary>
    private void ReleaseCurrentBody(EntityUid stationAi, EntityUid? currentBody, EntityUid nextBody)
    {
        if (currentBody is not { } currentBodyUid)
            return;

        if (currentBodyUid == nextBody)
            return;

        if (!TryComp<StationAiBodyComponent>(currentBodyUid, out var currentBodyComp))
            return;

        DoReleaseBody((currentBodyUid, currentBodyComp), stationAi);
    }

    /// <summary>
    /// Builds the display name used for an unoccupied AI body.
    /// </summary>
    private string GetFreeBodyName(int bodyNumber)
    {
        return Loc.GetString("station-ai-body-name", ("number", bodyNumber));
    }

    /// <summary>
    /// Returns the next body number after all existing AI bodies.
    /// </summary>
    private int GetNextBodyNumber()
    {
        var number = 0;
        var query = EntityQueryEnumerator<StationAiBodyComponent>();

        while (query.MoveNext(out _, out var body))
        {
            if (body.BodyNumber > number)
                number = body.BodyNumber;
        }

        return number + 1;
    }

    #endregion
}
