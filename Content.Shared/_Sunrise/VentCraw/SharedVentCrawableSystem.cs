using System.Linq;
using System.Numerics;
using Content.Shared._Sunrise.VentCraw.Components;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Emoting;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Throwing;
using Content.Shared.Tools.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.VentCraw;

/// <summary>
/// A system that handles the crawling behavior for vent creatures.
/// </summary>
public abstract partial class SharedVentCrawableSystem : EntitySystem
{
    [Dependency] private SharedVentTubeSystem _ventCrawTubeSystem = default!;
    [Dependency] private SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedTransformSystem _xformSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private SharedAudioSystem _audioSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VentCrawHolderComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<VentCrawHolderComponent, MoveInputEvent>(OnMoveInput);

        SubscribeLocalEvent<BeingVentCrawComponent, UseAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<BeingVentCrawComponent, PickupAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<BeingVentCrawComponent, ThrowAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<BeingVentCrawComponent, InteractionAttemptEvent>(OnInteractAttempt);
        SubscribeLocalEvent<BeingVentCrawComponent, PullAttemptEvent>(OnPullAttempt);
        SubscribeLocalEvent<BeingVentCrawComponent, AttackAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<BeingVentCrawComponent, ChangeDirectionAttemptEvent>(OnAttempt);
        // SubscribeLocalEvent<BeingVentCrawComponent, EmoteAttemptEvent>(OnEmoteAttempt);
        // SubscribeLocalEvent<BeingVentCrawComponent, SpeakAttemptEvent>(OnSpeakAttempt);
    }

    // SUNRISE-TODO: Не уверен что стоит блокировать эмоции и речь в трубах, но атмос-фелинид может знатно подзаебать.
    // private void OnSpeakAttempt(EntityUid uid, BeingVentCrawComponent component, SpeakAttemptEvent args)
    // {
    //     args.Cancel();
    // }
    //
    // private void OnEmoteAttempt(EntityUid uid, BeingVentCrawComponent component, EmoteAttemptEvent args)
    // {
    //     args.Cancel();
    // }

    private void OnInteractAttempt(Entity<BeingVentCrawComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnAttempt(EntityUid uid, BeingVentCrawComponent component, CancellableEntityEventArgs args)
    {
        args.Cancel();
    }

    private void OnPullAttempt(EntityUid uid, BeingVentCrawComponent component, PullAttemptEvent args)
    {
        args.Cancelled = true;
    }

    /// <summary>
    /// Handles the MoveInputEvent for VentCrawHolderComponent.
    /// </summary>
    /// <param name="uid">The EntityUid of the VentCrawHolderComponent.</param>
    /// <param name="component">The VentCrawHolderComponent instance.</param>
    /// <param name="holder"></param>
    /// <param name="args">The MoveInputEvent arguments.</param>
    private void OnMoveInput(EntityUid uid, VentCrawHolderComponent holder, ref MoveInputEvent args)
    {
        if (!Exists(holder.CurrentTube))
        {
            var ev = new VentCrawExitEvent();
            RaiseLocalEvent(uid, ref ev);
        }

        holder.IsMoving = args.Entity.Comp.HeldMoveButtons != MoveButtons.None;
        holder.CurrentDirection = DirVecForButtons(args.Entity.Comp.HeldMoveButtons).GetDir();
    }

    private Vector2 DirVecForButtons(MoveButtons buttons)
    {
        var x = 0;
        var y = 0;
        x -= HasFlag(buttons, MoveButtons.Left) ? 1 : 0;
        x += HasFlag(buttons, MoveButtons.Right) ? 1 : 0;
        y -= HasFlag(buttons, MoveButtons.Down) ? 1 : 0;
        y += HasFlag(buttons, MoveButtons.Up) ? 1 : 0;

        var vec = new Vector2(x, y);

        if (vec.LengthSquared() > 1.0e-6)
        {
            vec = vec.Normalized();
        }

        return vec;
    }

    private static bool HasFlag(MoveButtons buttons, MoveButtons flag)
    {
        return (buttons & flag) == flag;
    }

    /// <summary>
    /// Handles the ComponentStartup event for VentCrawHolderComponent.
    /// </summary>
    /// <param name="uid">The EntityUid of the VentCrawHolderComponent.</param>
    /// <param name="holder">The VentCrawHolderComponent instance.</param>
    /// <param name="args">The ComponentStartup arguments.</param>
    private void OnComponentStartup(EntityUid uid, VentCrawHolderComponent holder, ComponentStartup args)
    {
        holder.Container = _containerSystem.EnsureContainer<Container>(uid, nameof(VentCrawHolderComponent));
    }

    /// <summary>
    /// Tries to insert an entity into the VentCrawHolderComponent container.
    /// </summary>
    /// <param name="uid">The EntityUid of the VentCrawHolderComponent.</param>
    /// <param name="toInsert">The EntityUid of the entity to insert.</param>
    /// <param name="holder">The VentCrawHolderComponent instance.</param>
    /// <returns>True if the insertion was successful, otherwise False.</returns>
    public bool TryInsert(EntityUid uid, EntityUid toInsert, VentCrawHolderComponent? holder = null)
    {
        if (!Resolve(uid, ref holder))
            return false;

        if (!CanInsert(uid, toInsert, holder))
            return false;

        if (!_containerSystem.Insert(toInsert, holder.Container))
            return false;

        if (TryComp<PhysicsComponent>(toInsert, out var physBody))
            _physicsSystem.SetCanCollide(toInsert, false, body: physBody);

        return true;
    }

    /// <summary>
    /// Checks whether the specified entity can be inserted into the container of the VentCrawHolderComponent.
    /// </summary>
    /// <param name="uid">The EntityUid of the VentCrawHolderComponent.</param>
    /// <param name="toInsert">The EntityUid of the entity to be inserted.</param>
    /// <param name="holder">The VentCrawHolderComponent instance.</param>
    /// <returns>True if the entity can be inserted into the container; otherwise, False.</returns>
    private bool CanInsert(EntityUid uid, EntityUid toInsert, VentCrawHolderComponent? holder = null)
    {
        if (!Resolve(uid, ref holder))
            return false;

        if (!_containerSystem.CanInsert(toInsert, holder.Container))
            return false;

        return HasComp<ItemComponent>(toInsert) ||
            HasComp<BodyComponent>(toInsert);
    }

    /// <summary>
    /// Attempts to make the VentCrawHolderComponent enter a VentCrawTubeComponent.
    /// </summary>
    /// <param name="holderUid">The EntityUid of the VentCrawHolderComponent.</param>
    /// <param name="toUid">The EntityUid of the VentCrawTubeComponent to enter.</param>
    /// <param name="holder">The VentCrawHolderComponent instance.</param>
    /// <param name="holderTransform">The TransformComponent instance for the VentCrawHolderComponent.</param>
    /// <param name="to">The VentCrawTubeComponent instance to enter.</param>
    /// <param name="toTransform">The TransformComponent instance for the VentCrawTubeComponent.</param>
    /// <returns>True if the VentCrawHolderComponent successfully enters the VentCrawTubeComponent; otherwise, False.</returns>
    public bool EnterTube(EntityUid holderUid, EntityUid toUid, VentCrawHolderComponent? holder = null, TransformComponent? holderTransform = null, VentCrawTubeComponent? to = null, TransformComponent? toTransform = null)
    {
        if (!Resolve(holderUid, ref holder, ref holderTransform))
            return false;
        if (holder.IsExitingVentCraws)
        {
            Log.Error("Tried entering tube after exiting VentCraws. This should never happen.");
            return false;
        }
        if (!Resolve(toUid, ref to, ref toTransform))
        {
            var ev = new VentCrawExitEvent();
            RaiseLocalEvent(holderUid, ref ev);
            return false;
        }

        foreach (var ent in holder.Container.ContainedEntities)
        {
            var comp = EnsureComp<BeingVentCrawComponent>(ent);
            comp.Holder = holderUid;
        }

        if (!_containerSystem.Insert(holderUid, to.Contents))
        {
            var ev = new VentCrawExitEvent();
            RaiseLocalEvent(holderUid, ref ev);
            return false;
        }
        if (TryComp<PhysicsComponent>(holderUid, out var physBody))
            _physicsSystem.SetCanCollide(holderUid, false, body: physBody);

        if (holder.CurrentTube != null)
        {
            holder.PreviousTube = holder.CurrentTube;
            holder.PreviousDirection = holder.CurrentDirection;
        }
        holder.CurrentTube = toUid;

        return true;
    }

    /// <summary>
    ///  Magic...
    /// </summary>
    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<VentCrawHolderComponent>();
        while (query.MoveNext(out var uid, out var holder))
        {
            if (holder.CurrentDirection == Direction.Invalid || holder.CurrentTube == null)
                continue;

            var currentTube = holder.CurrentTube.Value;

            if (holder.IsMoving && holder.NextTube == null)
            {
                var nextTube = _ventCrawTubeSystem.NextTubeFor(currentTube, holder.CurrentDirection);

                if (nextTube != null)
                {
                    if (!Exists(holder.CurrentTube))
                    {
                        var ev = new VentCrawExitEvent();
                        RaiseLocalEvent(uid, ref ev);
                        continue;
                    }

                    holder.NextTube = nextTube;
                    holder.StartingTime = holder.Speed;
                    holder.TimeLeft = holder.Speed;
                }
                else
                {
                    var ev = new GetVentCrawsConnectableDirectionsEvent();
                    RaiseLocalEvent(currentTube, ref ev);
                    if (ev.Connectable.Contains(holder.CurrentDirection))
                    {
                        var Exitev = new VentCrawExitEvent();
                        RaiseLocalEvent(uid, ref Exitev);
                        continue;
                    }
                }
            }

            if (holder.NextTube != null && holder.TimeLeft > 0)
            {
                var time = frameTime;
                if (time > holder.TimeLeft)
                {
                    time = holder.TimeLeft;
                }

                var progress = 1 - holder.TimeLeft / holder.StartingTime;
                var origin = Transform(currentTube).Coordinates;
                var target = Transform(holder.NextTube.Value).Coordinates;
                var newPosition = (target.Position - origin.Position) * progress;

                _xformSystem.SetCoordinates(uid, origin.Offset(newPosition).WithEntityId(currentTube));

                holder.TimeLeft -= time;
                frameTime -= time;
            }
            else if (holder.NextTube != null && holder.TimeLeft == 0)
            {
                var welded = false;
                if (TryComp<WeldableComponent>(holder.NextTube.Value, out var weldableComponent))
                    welded = weldableComponent.IsWelded;
                if (HasComp<VentCrawEntryComponent>(holder.NextTube.Value) && !holder.FirstEntry && !welded)
                {
                    var ev = new VentCrawExitEvent();
                    RaiseLocalEvent(uid, ref ev);
                }
                else
                {
                    _containerSystem.Remove(uid, Comp<VentCrawTubeComponent>(currentTube).Contents ,reparent: false, force: true);

                    if (holder.FirstEntry)
                        holder.FirstEntry = false;

                    if (_gameTiming.CurTime > holder.LastCrawl + VentCrawHolderComponent.CrawlDelay)
                    {
                        holder.LastCrawl = _gameTiming.CurTime;
                        _audioSystem.PlayPvs(holder.CrawlSound, uid);
                    }

                    EnterTube(uid, holder.NextTube.Value, holder);
                    holder.NextTube = null;
                }
            }
        }
    }
}
