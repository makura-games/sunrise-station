using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.Effects;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Pulling.Events;
using Content.Shared.Speech;
using Content.Shared.Throwing;
using Content.Shared.Verbs;
using Content.Shared._Sunrise.Grab.Components;
using Content.Shared._Sunrise.Grab.Events;
using Content.Shared._Sunrise.Movement.Pulling;
using Content.Shared._Sunrise.Random;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Grab.Systems;

/// <summary>
/// Handles staged grabbing as a separate layer over pulling.
/// </summary>
public sealed partial class SharedGrabSystem : EntitySystem
{
    private static readonly SoundSpecifier GrabSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly RandomPredictedSystem _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtual = default!;
    private EntityQuery<GrabberComponent> _grabberQuery;
    private EntityQuery<GrabbedComponent> _grabbedQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<PullableComponent> _pullableQuery;
    private EntityQuery<PullerComponent> _pullerQuery;
    private EntityQuery<ActivePullingAnimationComponent> _pullingAnimationQuery;
    private EntityQuery<VirtualItemComponent> _virtualQuery;

    public override void Initialize()
    {
        base.Initialize();

        _grabberQuery = GetEntityQuery<GrabberComponent>();
        _grabbedQuery = GetEntityQuery<GrabbedComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _pullableQuery = GetEntityQuery<PullableComponent>();
        _pullerQuery = GetEntityQuery<PullerComponent>();
        _pullingAnimationQuery = GetEntityQuery<ActivePullingAnimationComponent>();
        _virtualQuery = GetEntityQuery<VirtualItemComponent>();

        SubscribeLocalEvent<PullableComponent, PullToggleAttemptEvent>(OnPullToggleAttempt);
        SubscribeLocalEvent<GetVerbsEvent<Verb>>(OnGetVerbs);

        SubscribeLocalEvent<GrabberComponent, StopPullingAlertEvent>(OnStopPullingAlert, before: [typeof(PullingSystem)]);
        SubscribeLocalEvent<GrabberComponent, PullStoppedMessage>(OnGrabberPullStopped);
        SubscribeLocalEvent<GrabberComponent, VirtualItemDeletedEvent>(OnVirtualItemDeleted, before: [typeof(PullingSystem)]);
        SubscribeLocalEvent<GrabberComponent, BeforeThrowEvent>(OnBeforeThrow);

        SubscribeLocalEvent<GrabbedComponent, StopBeingPulledAlertEvent>(OnStopBeingPulledAlert, before: [typeof(PullingSystem)]);
        SubscribeLocalEvent<GrabbedComponent, PullStoppedMessage>(OnGrabbedPullStopped);
        SubscribeLocalEvent<GrabbedComponent, AttemptStopPullingEvent>(OnAttemptStopPulling, before: [typeof(PullingSystem)]);
        SubscribeLocalEvent<GrabbedComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<GrabbedComponent, SpeakAttemptEvent>(OnSpeakAttempt);

        SubscribeLocalEvent<GrabThrownComponent, ThrowDoHitEvent>(OnGrabThrowHit);
        SubscribeLocalEvent<GrabThrownComponent, StopThrowEvent>(OnGrabStopThrow);
    }

    private void OnPullToggleAttempt(Entity<PullableComponent> ent, ref PullToggleAttemptEvent args)
    {
        if (args.Handled)
            return;

        var activeGrab = _grabberQuery.TryComp(args.Puller, out var grabber) &&
                         grabber.Grabbed == ent.Owner &&
                         grabber.Stage > GrabStage.No;

        if (!activeGrab && !_combatMode.IsInCombatMode(args.Puller))
            return;

        args.Handled = true;
        args.Result = TryStartOrTightenGrab(args.Puller, ent.Owner);
    }

    private void OnGetVerbs(GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_pullableQuery.TryComp(args.Target, out var pullable))
            return;

        if (pullable.Puller != args.User)
            return;

        if (!CanStartOrTightenGrab(args.User, args.Target, ignoreCombatMode: true, quiet: true))
            return;

        var user = args.User;
        var target = args.Target;
        var verb = new Verb
        {
            Text = Loc.GetString("pulling-verb-get-data-text-grab"),
            Act = () => TryStartOrTightenGrab(user, target, ignoreCombatMode: true),
            DoContactInteraction = false,
        };

        args.Verbs.Add(verb);
    }

    private void OnStopPullingAlert(Entity<GrabberComponent> ent, ref StopPullingAlertEvent args)
    {
        if (args.Handled || ent.Comp.Grabbed is not { } grabbed)
            return;

        args.Handled = TryHandleGrabRelaxRequest(ent.Owner, grabbed);
    }

    private void OnStopBeingPulledAlert(Entity<GrabbedComponent> ent, ref StopBeingPulledAlertEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryHandleGrabEscapeRequest(ent);
    }

    private void OnGrabberPullStopped(Entity<GrabberComponent> ent, ref PullStoppedMessage args)
    {
        if (ent.Comp.Grabbed != args.PulledUid)
            return;

        EndGrab(ent, (args.PulledUid, null));
    }

    private void OnGrabbedPullStopped(Entity<GrabbedComponent> ent, ref PullStoppedMessage args)
    {
        if (ent.Comp.Grabber != args.PullerUid)
            return;

        if (_grabberQuery.TryComp(args.PullerUid, out var grabber) && grabber.Grabbed == ent.Owner)
            return;

        CleanupGrabbedOnly(ent);
    }

    private void OnAttemptStopPulling(Entity<GrabbedComponent> ent, ref AttemptStopPullingEvent args)
    {
        if (ent.Comp.Stage <= GrabStage.No || args.User == null)
            return;

        if (args.User == ent.Comp.Grabber)
        {
            if (TryHandleGrabRelaxRequest(ent.Comp.Grabber, ent.Owner))
                args.Cancelled = true;
            return;
        }

        if (args.User != ent.Owner)
            return;

        args.Cancelled = true;
        TryHandleGrabEscapeRequest(ent, predictedSelfPopup: false);
    }

    private void OnUpdateCanMove(Entity<GrabbedComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.Stage >= GrabStage.Hard)
            args.Cancel();
    }

    private void OnSpeakAttempt(Entity<GrabbedComponent> ent, ref SpeakAttemptEvent args)
    {
        if (ent.Comp.Stage != GrabStage.Suffocate)
            return;

        args.Cancel();
        PopupGrabActor(Loc.GetString("popup-grabbed-cant-speak"), ent.Owner, ent.Owner, PopupType.MediumCaution);
    }

    /// <summary>
    /// Attempts to start a soft grab, tighten an existing grab, or apply suffocation pressure at max stage.
    /// </summary>
    public bool TryStartOrTightenGrab(EntityUid grabberUid, EntityUid grabbedUid, bool ignoreCombatMode = false)
    {
        if (!CanStartOrTightenGrab(grabberUid, grabbedUid, ignoreCombatMode))
            return false;

        var hadGrabber = _grabberQuery.TryComp(grabberUid, out var grabber);
        var hadGrabbed = _grabbedQuery.TryComp(grabbedUid, out var grabbed);

        grabber ??= EnsureComp<GrabberComponent>(grabberUid);
        grabbed ??= EnsureComp<GrabbedComponent>(grabbedUid);

        var currentStage = grabber.Grabbed == grabbedUid ? grabber.Stage : GrabStage.No;
        if (currentStage >= GrabStage.Suffocate)
        {
            _stamina.TakeStaminaDamage(grabbedUid, grabber.SuffocateStaminaDamage, source: grabberUid);
            grabber.NextStageChange = _timing.CurTime + grabber.StageChangeCooldown;
            Dirty(grabberUid, grabber);
            return true;
        }

        var nextStage = (GrabStage)((byte)currentStage + 1);
        if (TrySetGrabStage((grabberUid, grabber), (grabbedUid, grabbed), nextStage, GrabStageChangeCause.Tighten))
            return true;

        if (!hadGrabber)
            RemCompDeferred<GrabberComponent>(grabberUid);

        if (!hadGrabbed)
            RemCompDeferred<GrabbedComponent>(grabbedUid);

        return false;
    }

    /// <summary>
    /// Attempts to make the grabbed entity struggle free by one grab stage using its configured escape chance.
    /// </summary>
    public bool TryEscapeGrab(
        EntityUid grabbedUid,
        GrabbedComponent? grabbed = null,
        bool quiet = false,
        bool predictedSelfPopup = true)
    {
        if (!Resolve(grabbedUid, ref grabbed, false) ||
            grabbed.Grabber == EntityUid.Invalid ||
            grabbed.Stage <= GrabStage.No)
        {
            return false;
        }

        if (grabbed.NextEscapeAttempt > _timing.CurTime)
            return false;

        grabbed.NextEscapeAttempt = _timing.CurTime + grabbed.EscapeAttemptCooldown;
        Dirty(grabbedUid, grabbed);

        var escapeChance = GetCurrentEscapeChance(grabbed);
        if (!_random.ProbForEntity(grabbedUid, escapeChance))
        {
            grabbed.EscapeChanceBonus = Math.Clamp(grabbed.EscapeChanceBonus + grabbed.EscapeChanceBonusPerFail, 0f, 1f);
            Dirty(grabbedUid, grabbed);

            if (!quiet)
                ShowEscapeFailPopup((grabbedUid, grabbed), predictedSelfPopup);

            return false;
        }

        return TryLowerGrabStage(grabbed.Grabber, grabbedUid, GrabStageChangeCause.Escape, predictedSelfPopup);
    }

    /// <summary>
    /// Checks whether <paramref name="grabberUid"/> can start or strengthen a grab on <paramref name="grabbedUid"/>.
    /// </summary>
    public bool CanStartOrTightenGrab(
        EntityUid grabberUid,
        EntityUid grabbedUid,
        bool ignoreCombatMode = false,
        bool quiet = false)
    {
        if (!_pullerQuery.TryComp(grabberUid, out var puller) || puller.Pulling != grabbedUid)
            return false;

        if (!_pullableQuery.TryComp(grabbedUid, out var pullable) || pullable.Puller != grabberUid)
            return false;

        if (!HasComp<MobStateComponent>(grabbedUid))
            return false;

        if (!_blocker.CanInteract(grabberUid, grabbedUid))
            return false;

        if (_grabbedQuery.TryComp(grabberUid, out var grabbed) &&
            grabbed.Stage > GrabStage.No &&
            !HasComp<GrabWhileGrabbedComponent>(grabberUid))
        {
            if (!quiet)
                PopupGrabActor(Loc.GetString("popup-grab-cannot-while-grabbed"), grabberUid, grabberUid, PopupType.SmallCaution);

            return false;
        }

        var activeGrab = _grabberQuery.TryComp(grabberUid, out var grabber) &&
                         grabber.Grabbed == grabbedUid &&
                         grabber.Stage > GrabStage.No;

        if (!ignoreCombatMode && !activeGrab && !_combatMode.IsInCombatMode(grabberUid))
            return false;

        if (grabber == null || grabber.NextStageChange <= _timing.CurTime)
            return true;

        if (!quiet)
            PopupGrabActor(Loc.GetString("popup-grab-retighten-cooldown"), grabbedUid, grabberUid, PopupType.SmallCaution);

        return false;
    }

    /// <summary>
    /// Lowers the current grab stage. Dropping from soft releases the grab but keeps the underlying pull.
    /// </summary>
    private bool TryLowerGrabStage(
        EntityUid grabberUid,
        EntityUid grabbedUid,
        GrabStageChangeCause cause = GrabStageChangeCause.Relax,
        bool predictedSelfPopup = true)
    {
        if (!_grabberQuery.TryComp(grabberUid, out var grabber) ||
            !_grabbedQuery.TryComp(grabbedUid, out var grabbed) ||
            grabber.Grabbed != grabbedUid ||
            grabbed.Grabber != grabberUid ||
            grabber.Stage <= GrabStage.No)
        {
            return false;
        }

        var nextStage = grabber.Stage <= GrabStage.Soft
            ? GrabStage.No
            : (GrabStage)((byte)grabber.Stage - 1);

        return TrySetGrabStage((grabberUid, grabber), (grabbedUid, grabbed), nextStage, cause, predictedSelfPopup);
    }

    private bool TryHandleGrabRelaxRequest(EntityUid grabberUid, EntityUid grabbedUid)
    {
        if (!_grabberQuery.TryComp(grabberUid, out var grabber) ||
            grabber.Grabbed != grabbedUid ||
            grabber.Stage <= GrabStage.No)
        {
            return false;
        }

        return TryLowerGrabStage(grabberUid, grabbedUid, GrabStageChangeCause.Relax);
    }

    private bool TryHandleGrabEscapeRequest(
        Entity<GrabbedComponent> grabbed,
        bool quiet = false,
        bool predictedSelfPopup = true)
    {
        if (grabbed.Comp.Grabber == EntityUid.Invalid || grabbed.Comp.Stage <= GrabStage.No)
            return false;

        TryEscapeGrab(grabbed.Owner, grabbed.Comp, quiet, predictedSelfPopup);
        return true;
    }

    private bool TrySetGrabStage(
        Entity<GrabberComponent> grabber,
        Entity<GrabbedComponent> grabbed,
        GrabStage stage,
        GrabStageChangeCause cause,
        bool predictedSelfPopup = true)
    {
        var oldStage = grabber.Comp.Grabbed == grabbed.Owner
            ? grabber.Comp.Stage
            : GrabStage.No;

        if (stage <= GrabStage.No)
        {
            ShowGrabStageChangePopup(grabber.Owner, grabbed.Owner, oldStage, GrabStage.No, cause, predictedSelfPopup);
            EndGrab(grabber, grabbed.AsNullable());
            return true;
        }

        if (!TryUpdateGrabVirtualItems(grabber, grabbed.Owner, stage))
            return false;

        grabber.Comp.Grabbed = grabbed.Owner;
        grabber.Comp.Stage = stage;
        grabber.Comp.NextStageChange = _timing.CurTime + grabber.Comp.StageChangeCooldown;

        grabbed.Comp.Grabber = grabber.Owner;
        grabbed.Comp.Stage = stage;
        grabbed.Comp.EscapeChance = GetEscapeChance(grabber.Comp, stage);
        grabbed.Comp.EscapeChanceBonus = 0f;
        grabbed.Comp.NextEscapeAttempt = _timing.CurTime + grabbed.Comp.EscapeAttemptCooldown;

        Dirty(grabber);
        Dirty(grabbed);

        UpdateGrabEffectVisuals(grabbed.Owner, stage);

        _alerts.ShowAlert(grabber.Owner, grabber.Comp.GrabbingAlert, GetSeverity(stage));
        _alerts.ShowAlert(grabbed.Owner, grabbed.Comp.GrabbedAlert, GetSeverity(stage));
        _blocker.UpdateCanMove(grabbed.Owner);
        _movementSpeed.RefreshMovementSpeedModifiers(grabber.Owner);

        ShowGrabStageChangePopup(grabber.Owner, grabbed.Owner, oldStage, stage, cause, predictedSelfPopup);

        if (cause == GrabStageChangeCause.Tighten)
        {
            _audio.PlayPredicted(GrabSound, grabbed.Owner, grabber.Owner);
            var flashColor = stage switch
            {
                GrabStage.Hard => Color.FromHex("#FF8080"),
                GrabStage.Suffocate => Color.Red,
                _ => Color.Yellow,
            };
            _color.RaiseEffect(flashColor, new List<EntityUid> { grabbed.Owner }, Filter.Pvs(grabbed.Owner, entityManager: EntityManager));
        }

        return true;
    }

    private void EndGrab(Entity<GrabberComponent> grabber, Entity<GrabbedComponent?> grabbed)
    {
        Resolve(grabbed, ref grabbed.Comp, false);

        if (grabbed.Comp != null && !TerminatingOrDeleted(grabbed.Owner))
            ClearGrabbedState((grabbed.Owner, grabbed.Comp));

        CleanupGrabVirtualItems(grabber);

        _alerts.ClearAlert(grabber.Owner, grabber.Comp.GrabbingAlert);
        if (!TerminatingOrDeleted(grabbed.Owner))
            _alerts.ClearAlert(grabbed.Owner, grabbed.Comp?.GrabbedAlert ?? "Grabbed");

        if (!TerminatingOrDeleted(grabbed.Owner))
            _blocker.UpdateCanMove(grabbed.Owner);
        _movementSpeed.RefreshMovementSpeedModifiers(grabber.Owner);

        RemCompDeferred<GrabberComponent>(grabber.Owner);
        if (!TerminatingOrDeleted(grabbed.Owner))
            RemCompDeferred<GrabbedComponent>(grabbed.Owner);
    }

    private void CleanupGrabbedOnly(Entity<GrabbedComponent> grabbed)
    {
        ClearGrabbedState(grabbed);
        _alerts.ClearAlert(grabbed.Owner, grabbed.Comp.GrabbedAlert);
        _blocker.UpdateCanMove(grabbed.Owner);
        RemCompDeferred<GrabbedComponent>(grabbed.Owner);
    }

    private void ClearGrabbedState(Entity<GrabbedComponent> grabbed)
    {
        grabbed.Comp.Grabber = EntityUid.Invalid;
        grabbed.Comp.Stage = GrabStage.No;
        grabbed.Comp.EscapeChance = 1f;
        grabbed.Comp.EscapeChanceBonus = 0f;
        grabbed.Comp.NextEscapeAttempt = TimeSpan.Zero;
        Dirty(grabbed);

        UpdateGrabEffectVisuals(grabbed.Owner, GrabStage.No);
    }

    public void UpdateGrabEffectVisuals(EntityUid grabbedUid, GrabStage stage)
    {
        if (_pullingAnimationQuery.TryComp(grabbedUid, out var anim) && anim.Effect is { } effect)
            _appearance.SetData(effect, GrabVisuals.Stage, stage);
    }

    private static float GetEscapeChance(GrabberComponent grabber, GrabStage stage)
    {
        return grabber.EscapeChances.TryGetValue(stage, out var chance) ? chance : 1f;
    }

    private static float GetCurrentEscapeChance(GrabbedComponent grabbed)
    {
        return Math.Clamp(grabbed.EscapeChance + grabbed.EscapeChanceBonus, 0f, 1f);
    }

    private static short GetSeverity(GrabStage stage)
    {
        return (short)stage;
    }

    private static string GetStageSuffix(GrabStage stage)
    {
        return stage switch
        {
            GrabStage.No => "no",
            GrabStage.Soft => "soft",
            GrabStage.Hard => "hard",
            GrabStage.Suffocate => "suffocate",
            _ => "no",
        };
    }

    private static PopupType GetStagePopupType(GrabStage stage)
    {
        return stage switch
        {
            GrabStage.No => PopupType.Small,
            GrabStage.Soft => PopupType.Small,
            GrabStage.Hard => PopupType.MediumCaution,
            GrabStage.Suffocate => PopupType.LargeCaution,
            _ => PopupType.Small,
        };
    }

    private void ShowGrabStageChangePopup(
        EntityUid grabber,
        EntityUid grabbed,
        GrabStage oldStage,
        GrabStage newStage,
        GrabStageChangeCause cause,
        bool predictedSelfPopup = true)
    {
        if (oldStage == newStage)
            return;

        var suffix = GetStageSuffix(newStage);
        var popupType = GetStagePopupType(newStage);

        switch (cause)
        {
            case GrabStageChangeCause.Tighten:
                ShowGrabActionPopup(
                    grabbed,
                    grabber,
                    grabbed,
                    grabber,
                    grabbed,
                    $"popup-grab-{suffix}-self",
                    $"popup-grab-{suffix}-target",
                    $"popup-grab-{suffix}-others",
                    popupType,
                    predictedSelfPopup);
                break;
            case GrabStageChangeCause.Relax:
                ShowGrabActionPopup(
                    grabbed,
                    grabber,
                    grabbed,
                    grabber,
                    grabbed,
                    $"popup-grab-{suffix}-relax-self",
                    $"popup-grab-{suffix}-relax-target",
                    $"popup-grab-{suffix}-relax-others",
                    popupType,
                    predictedSelfPopup);
                break;
            case GrabStageChangeCause.Escape:
                ShowGrabActionPopup(
                    grabbed,
                    grabbed,
                    grabber,
                    grabber,
                    grabbed,
                    $"popup-grab-{suffix}-escape-self",
                    $"popup-grab-{suffix}-escape-target",
                    $"popup-grab-{suffix}-escape-others",
                    popupType,
                    predictedSelfPopup);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(cause), cause, null);
        }
    }

    private void ShowGrabActionPopup(
        EntityUid popupUid,
        EntityUid predictingUser,
        EntityUid otherRecipient,
        EntityUid grabber,
        EntityUid grabbed,
        string selfKey,
        string targetKey,
        string othersKey,
        PopupType popupType,
        bool predictedSelfPopup = true)
    {
        var selfMessage = Loc.GetString(selfKey,
            ("puller", Identity.Entity(grabber, EntityManager)),
            ("target", Identity.Entity(grabbed, EntityManager)));
        var targetMessage = Loc.GetString(targetKey,
            ("puller", Identity.Entity(grabber, EntityManager)),
            ("target", Identity.Entity(grabbed, EntityManager)));
        var othersMessage = Loc.GetString(othersKey,
            ("puller", Identity.Entity(grabber, EntityManager)),
            ("target", Identity.Entity(grabbed, EntityManager)));

        PopupGrabActor(selfMessage, popupUid, predictingUser, popupType, predictedSelfPopup);
        PopupGrabTarget(targetMessage, popupUid, otherRecipient, predictingUser, popupType);
        PopupGrabObservers(othersMessage, popupUid, grabber, grabbed, popupType);
    }

    private void ShowEscapeFailPopup(Entity<GrabbedComponent> grabbed, bool predictedSelfPopup = true)
    {
        PopupGrabActor(
            Loc.GetString("popup-grab-release-fail-self"),
            grabbed.Owner,
            grabbed.Owner,
            PopupType.SmallCaution,
            predictedSelfPopup);
    }

    private void PopupGrabActor(
        string message,
        EntityUid popupUid,
        EntityUid recipient,
        PopupType type,
        bool predicted = true)
    {
        if (predicted)
        {
            _popup.PopupPredicted(message, popupUid, recipient, Filter.Empty(), true, type);
            return;
        }

        if (_net.IsClient)
            return;

        _popup.PopupEntity(message, popupUid, recipient, type);
    }

    private void PopupGrabTarget(string message, EntityUid popupUid, EntityUid recipient, EntityUid predictingUser, PopupType type)
    {
        if (_net.IsClient)
        {
            if (_timing.IsFirstTimePredicted && recipient == predictingUser)
                _popup.PopupEntity(message, popupUid, recipient, type);

            return;
        }

        if (recipient == predictingUser)
            return;

        _popup.PopupEntity(message, popupUid, recipient, type);
    }

    private void PopupGrabObservers(string message, EntityUid popupUid, EntityUid grabber, EntityUid grabbed, PopupType type)
    {
        if (_net.IsClient)
            return;

        var filter = Filter.Empty()
            .AddPlayersByPvs(grabber, entityManager: EntityManager)
            .RemovePlayerByAttachedEntity(grabber)
            .RemovePlayerByAttachedEntity(grabbed);

        _popup.PopupEntity(message, popupUid, filter, true, type);
    }

    private enum GrabStageChangeCause
    {
        Tighten,
        Relax,
        Escape,
    }
}
