using System.Numerics;
using Content.Shared.Coordinates;
using Content.Shared.DoAfter;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Content.Shared.Throwing;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.Hands;
using Content.Shared.Standing;
using Content.Shared.Movement.Events;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Climbing.Events;
using Content.Shared.Buckle.Components;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Content.Shared._Sunrise.Movement.Carrying.Slowdown;

namespace Content.Shared._Sunrise.Movement.Carrying;

public abstract partial class SharedCarryingSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CanBeCarriedComponent, GetVerbsEvent<AlternativeVerb>>(AddCarryVerb);
        SubscribeLocalEvent<CanBeCarriedComponent, CarryDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<CarrierComponent, ComponentShutdown>(OnCarrierShutdown);
        SubscribeLocalEvent<ActiveCarrierComponent, ComponentShutdown>(OnActiveCarrierShutdown);
        SubscribeLocalEvent<ActiveCanBeCarriedComponent, ComponentShutdown>(OnActiveCanBeCarriedShutdown);

        SubscribeLocalEvent<ActiveCarrierComponent, BeforeThrowEvent>(OnBeforeThrow);
        SubscribeLocalEvent<ActiveCarrierComponent, VirtualItemDeletedEvent>(OnVirtualItemDeleted);
        SubscribeLocalEvent<ActiveCarrierComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<ActiveCarrierComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ActiveCarrierComponent, MoveEvent>(OnCarrierMove);
        SubscribeLocalEvent<ActiveCarrierComponent, DownedEvent>(OnCarrierDowned);
        SubscribeLocalEvent<ActiveCarrierComponent, PullStartedMessage>(OnCarrierPullStarted);
        SubscribeLocalEvent<ActiveCarrierComponent, PullStoppedMessage>(OnCarrierPullStopped);

        SubscribeLocalEvent<ActiveCanBeCarriedComponent, UpdateCanMoveEvent>(OnMoveAttempt);
        SubscribeLocalEvent<ActiveCanBeCarriedComponent, StandAttemptEvent>(OnStandAttempt);
        SubscribeLocalEvent<ActiveCanBeCarriedComponent, GettingInteractedWithAttemptEvent>(OnInteractedWith);
        SubscribeLocalEvent<ActiveCanBeCarriedComponent, PullAttemptEvent>(OnPullAttempt);
        SubscribeLocalEvent<ActiveCanBeCarriedComponent, StartClimbEvent>(OnStartClimb);
        SubscribeLocalEvent<ActiveCanBeCarriedComponent, BuckledEvent>(OnBuckleChange);
        SubscribeLocalEvent<ActiveCanBeCarriedComponent, InteractionAttemptEvent>(OnInteractionAttempt);
    }

    #region Update

    /// <inheritdoc/>
    public override void Update(float frametime)
    {
        base.Update(frametime);

        var query = EntityQueryEnumerator<ActiveCarrierComponent, CarrierComponent>();
        while (query.MoveNext(out var uid, out var comp, out var carrier))
        {
            if (comp.Target == null)
                continue;

            var target = comp.Target.Value;

            if (!Exists(target) || Deleted(target) || Terminating(target))
            {
                RemComp<ActiveCarrierComponent>(uid);
                continue;
            }

            if (!TryComp(target, out TransformComponent? targetXform))
            {
                RemComp<ActiveCarrierComponent>(uid);
                continue;
            }

            var expectedCoordinates = GetCarriedCoordinates(uid);

            if (!expectedCoordinates.TryDistance(EntityManager, targetXform.Coordinates, out var distance)
                || distance > carrier.MaxSeparation)
            {
                DropCarried(uid, target);
            }
        }
    }

    #endregion

    #region Event handlers

    private void AddCarryVerb(Entity<CanBeCarriedComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        if (!CanCarry(args.User, ent.AsNullable()))
            return;

        var user = args.User;
        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                StartCarryDoAfter(user, ent.AsNullable());
            },
            Text = Loc.GetString("carry-verb"),
            Priority = 2,
            Icon = ent.Comp.VerbIcon,
        };
        args.Verbs.Add(verb);
    }

    private void OnDoAfter(Entity<CanBeCarriedComponent> ent, ref CarryDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        TryStartCarry(args.Args.User, ent.AsNullable());
        args.Handled = true;
    }

    private void OnCarrierShutdown(Entity<CarrierComponent> ent, ref ComponentShutdown args)
    {
        if (_timing.ApplyingState)
            return;

        if (!TryComp<ActiveCarrierComponent>(ent, out var activeCarrier) || activeCarrier.Target == null)
            return;

        var target = activeCarrier.Target.Value;
        DropCarried(ent.Owner, target);
    }

    private void OnActiveCarrierShutdown(Entity<ActiveCarrierComponent> ent, ref ComponentShutdown args)
    {
        if (_timing.ApplyingState)
            return;

        RemCompDeferred<CarryingSlowdownComponent>(ent.Owner);
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);

        if (ent.Comp.Target == null)
            return;

        var target = ent.Comp.Target.Value;

        if (TryComp<ActiveCanBeCarriedComponent>(target, out var activeCanBeCarried))
        {
            if (activeCanBeCarried.Carrier == ent.Owner)
            {
                activeCanBeCarried.Carrier = null;
                RemCompDeferred<ActiveCanBeCarriedComponent>(target);
            }
        }
    }

    private void OnActiveCanBeCarriedShutdown(Entity<ActiveCanBeCarriedComponent> ent, ref ComponentShutdown args)
    {
        if (_timing.ApplyingState)
            return;

        if (ent.Comp.Carrier == null)
            return;

        var carrier = ent.Comp.Carrier.Value;
        if (!Exists(carrier) || Deleted(carrier))
            return;

        if (TryComp<ActiveCarrierComponent>(carrier, out var activeCarrier))
        {
            if (activeCarrier.Target == ent.Owner)
            {
                activeCarrier.Target = null;
                RemCompDeferred<ActiveCarrierComponent>(carrier);
            }
        }
    }

    private void OnBeforeThrow(Entity<ActiveCarrierComponent> ent, ref BeforeThrowEvent args)
    {
        if (args.Direction == Vector2.Zero)
            return;

        if (!TryComp<VirtualItemComponent>(args.ItemUid, out var item) || !HasComp<CanBeCarriedComponent>(item.BlockingEntity))
            return;

        if (!TryComp<CarrierComponent>(ent, out var carrier))
            return;

        var target = item.BlockingEntity;
        var direction = args.Direction.Normalized();
        var massRatio = MassContest(ent.Owner, target);
        var throwSpeed = CalculateThrowSpeed(args.ThrowSpeed, massRatio, carrier);
        var throwDistance = CalculateThrowDistance(throwSpeed, carrier);

        if (!TryDropCarried(ent.AsNullable()))
            return;

        _throwing.TryThrow(
            target,
            direction * throwDistance,
            throwSpeed,
            args.PlayerUid,
            pushbackRatio: 0f,
            compensateFriction: true,
            doSpin: false);

        args.Cancelled = true;
    }

    private void OnVirtualItemDeleted(Entity<ActiveCarrierComponent> ent, ref VirtualItemDeletedEvent args)
    {
        if (ent.Comp.Target != args.BlockingEntity)
            return;

        TryDropCarried(ent.AsNullable());
    }

    private void OnParentChanged(Entity<ActiveCarrierComponent> ent, ref EntParentChangedMessage args)
    {
        // Do not drop the carried entity if the new parent is a grid
        var xform = args.Transform;
        if (xform.ParentUid == xform.GridUid)
            return;

        TryDropCarried(ent.AsNullable());
    }

    private void OnMobStateChanged(Entity<ActiveCarrierComponent> ent, ref MobStateChangedEvent args)
    {
        TryDropCarried(ent.AsNullable());
    }

    private void OnCarrierMove(Entity<ActiveCarrierComponent> ent, ref MoveEvent args)
    {
        if (ent.Comp.Target == null)
            return;

        var target = ent.Comp.Target.Value;

        if (!Exists(target) || Deleted(target) || Terminating(target))
            return;

        UpdateCarriedTransform(ent.Owner, target);
    }

    private void OnCarrierDowned(Entity<ActiveCarrierComponent> ent, ref DownedEvent args)
    {
        if (ent.Comp.Target == null)
            return;

        var target = ent.Comp.Target.Value;
        _popup.PopupClient(Loc.GetString("carry-lying-cancel"), target, ent.Owner, PopupType.MediumCaution);
        DropCarried(ent.Owner, target);
    }

    private void OnCarrierPullStarted(Entity<ActiveCarrierComponent> ent, ref PullStartedMessage args)
    {
        RefreshCarriedTransform(ent);
    }

    private void OnCarrierPullStopped(Entity<ActiveCarrierComponent> ent, ref PullStoppedMessage args)
    {
        RefreshCarriedTransform(ent);
    }

    private void OnMoveAttempt(Entity<ActiveCanBeCarriedComponent> ent, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnStandAttempt(Entity<ActiveCanBeCarriedComponent> ent, ref StandAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnInteractedWith(Entity<ActiveCanBeCarriedComponent> ent, ref GettingInteractedWithAttemptEvent args)
    {
        if (args.Uid != ent.Comp.Carrier)
            args.Cancelled = true;
    }

    private void OnPullAttempt(Entity<ActiveCanBeCarriedComponent> ent, ref PullAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnStartClimb(Entity<ActiveCanBeCarriedComponent> ent, ref StartClimbEvent args)
    {
        TryDropCarriedByTarget(ent.AsNullable());
    }

    private void OnBuckleChange(Entity<ActiveCanBeCarriedComponent> ent, ref BuckledEvent args)
    {
        TryDropCarriedByTarget(ent.AsNullable());
    }

    private void OnInteractionAttempt(Entity<ActiveCanBeCarriedComponent> ent, ref InteractionAttemptEvent args)
    {
        if (args.Target == null)
            return;

        if (!TryComp(args.Target.Value, out TransformComponent? targetXform))
            return;

        var targetParent = targetXform.ParentUid;

        if (args.Target.Value != ent.Owner &&
            args.Target.Value != ent.Comp.Carrier &&
            targetParent != ent.Comp.Carrier &&
            targetParent != ent.Owner)
            args.Cancelled = true;
    }

    #endregion

    private void StartCarryDoAfter(Entity<CarrierComponent?> carrier, Entity<CanBeCarriedComponent?> carried)
    {
        if (!Resolve(carrier, ref carrier.Comp, false) ||
            !Resolve(carried, ref carried.Comp, false))
            return;

        var length = carrier.Comp.BasePickupTime;

        var mod = MassContest(carrier.Owner, carried.Owner);

        if (mod > 0)
            length /= mod;

        if (!HasComp<KnockedDownComponent>(carried))
            length *= carried.Comp.StandingPickupTimeMultiplier;

        if (TryComp<MobStateComponent>(carried, out var mobState) && mobState.CurrentState != MobState.Alive)
            length *= carried.Comp.IncapacitatedPickupTimeMultiplier;

        if (length >= carrier.Comp.MaxPickupTime)
        {
            _popup.PopupPredicted(Loc.GetString("carry-too-heavy"), carried, carrier, PopupType.SmallCaution);
            return;
        }

        var ev = new CarryDoAfterEvent();
        var args = new DoAfterArgs(EntityManager, carrier, length, ev, carried, target: carried)
        {
            BreakOnMove = true,
            NeedHand = true,
            MovementThreshold = carrier.Comp.PickupMovementThreshold,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return;

        ShowCarryPopup("carry-starting", Filter.Entities(carrier), PopupType.Medium, carrier, carried);
        ShowCarryPopup("carry-started", Filter.Entities(carried), PopupType.Medium, carrier, carried);
        ShowCarryPopup("carry-observed", Filter.PvsExcept(carrier).RemoveWhereAttachedEntity(e => e == carried.Owner), PopupType.MediumCaution, carrier, carried);
    }

    private static EntityCoordinates GetCarriedCoordinates(EntityUid carrier)
    {
        return carrier.ToCoordinates();
    }

    private void RefreshCarriedTransform(Entity<ActiveCarrierComponent> ent)
    {
        if (ent.Comp.Target == null)
            return;

        var target = ent.Comp.Target.Value;

        if (!Exists(target) || Deleted(target))
            return;

        UpdateCarriedTransform(ent.Owner, target);
    }

    private void UpdateCarriedTransform(EntityUid carrier, EntityUid target)
    {
        if (!TryComp(target, out TransformComponent? targetXform))
            return;

        var carrierRotation = _transform.GetWorldRotation(carrier);
        _transform.SetCoordinates(target, targetXform, GetCarriedCoordinates(carrier), rotation: Angle.Zero - carrierRotation);
    }
}
