using Content.Shared._Sunrise.Movement.Carrying.Slowdown;
using Content.Shared.ActionBlocker;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Whitelist;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Shared._Sunrise.Movement.Carrying;

public abstract partial class SharedCarryingSystem
{
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private CarryingSlowdownSystem _slowdown = default!;
    [Dependency] private SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    #region Start carrying

    /// <summary>
    /// Tries to start carrying a target entity.
    /// </summary>
    /// <param name="carrier">Entity that should carry the target.</param>
    /// <param name="target">Entity that should be carried.</param>
    /// <returns>True if carrying was started successfully.</returns>
    [PublicAPI]
    public bool TryStartCarry(Entity<CarrierComponent?> carrier, Entity<CanBeCarriedComponent?> target)
    {
        if (!Resolve(carrier, ref carrier.Comp, false) ||
            !Resolve(target, ref target.Comp, false))
            return false;

        if (!CanCarry(carrier, target))
            return false;

        StartCarry(carrier!, target!);
        return true;
    }

    /// <summary>
    /// Checks whether the carrier is currently allowed to carry the target.
    /// </summary>
    /// <param name="carrier">Entity that wants to carry the target.</param>
    /// <param name="target">Entity that would be carried.</param>
    /// <returns>True if the carry action can be started.</returns>
    protected bool CanCarry(Entity<CarrierComponent?> carrier, Entity<CanBeCarriedComponent?> target)
    {
        if (carrier.Owner == target.Owner)
            return false;

        if (!HasComp<MapGridComponent>(Transform(carrier).ParentUid))
            return false;

        if (HasComp<ActiveCanBeCarriedComponent>(carrier) || HasComp<ActiveCanBeCarriedComponent>(target))
            return false;

        if (!Resolve(carrier, ref carrier.Comp, false) ||
            !Resolve(target, ref target.Comp, false))
            return false;

        if (!_whitelist.CheckBoth(target, carrier.Comp.TargetBlacklist, carrier.Comp.TargetWhitelist) ||
            !_whitelist.CheckBoth(carrier, target.Comp.CarrierBlacklist, target.Comp.CarrierWhitelist))
            return false;

        if (!_mobState.IsAlive(carrier.Owner))
            return false;

        if (_hands.CountFreeHands(carrier.Owner) < target.Comp.FreeHandsRequired)
            return false;

        if (!_interaction.InRangeUnobstructed(carrier.Owner, target.Owner, carrier.Comp.InteractionRange))
            return false;

        var targetEv = new StartBeingCarryAttemptEvent(carrier);
        RaiseLocalEvent(target, ref targetEv);
        if (targetEv.Cancelled)
            return false;

        var carrierEv = new StartCarryAttemptEvent(target);
        RaiseLocalEvent(carrier, ref carrierEv);
        if (carrierEv.Cancelled)
            return false;

        return true;
    }

    private void StartCarry(Entity<CarrierComponent> carrier, Entity<CanBeCarriedComponent> target)
    {
        var carrierUid = carrier.Owner;
        var targetUid = target.Owner;

        if (HasComp<ActiveCarrierComponent>(carrierUid))
            TryDropCarried(carrierUid);

        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(targetUid, pullable, carrierUid);

        var activeCarrier = EnsureComp<ActiveCarrierComponent>(carrierUid);
        var activeCanBeCarried = EnsureComp<ActiveCanBeCarriedComponent>(targetUid);
        activeCarrier.Target = targetUid;
        activeCanBeCarried.Carrier = carrierUid;
        Dirty(carrierUid, activeCarrier);
        Dirty(targetUid, activeCanBeCarried);

        ApplySlowdown(carrier, target);

        _standing.Down(targetUid, playSound: false, dropHeldItems: false);
        var knockedDown = EnsureComp<KnockedDownComponent>(targetUid);
        _stun.SetAutoStand((targetUid, knockedDown));

        _transform.AttachToGridOrMap(carrierUid);
        UpdateCarriedTransform(carrierUid, targetUid);

        for (var i = 0; i < target.Comp.FreeHandsRequired; i++)
        {
            _virtualItem.TrySpawnVirtualItemInHand(targetUid, carrierUid);
        }

        _actionBlocker.UpdateCanMove(targetUid);
        OnCarryStarted(carrierUid, targetUid);
    }

    #endregion

    #region Drop carried

    /// <summary>
    /// Tries to drop this entity from its current carrier.
    /// </summary>
    /// <param name="ent">Entity currently being carried.</param>
    /// <returns>True if the carried entity was dropped.</returns>
    [PublicAPI]
    public bool TryDropCarriedByTarget(Entity<ActiveCanBeCarriedComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (ent.Comp.Carrier == null)
            return false;

        return TryDropCarried(ent.Comp.Carrier.Value);
    }

    /// <summary>
    /// Tries to drop the entity currently carried by this carrier.
    /// </summary>
    /// <param name="ent">Entity currently carrying another entity.</param>
    /// <returns>True if a carried entity was dropped.</returns>
    [PublicAPI]
    public bool TryDropCarried(Entity<ActiveCarrierComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return false;

        if (ent.Comp.Target == null)
            return false;

        DropCarried(ent, ent.Comp.Target.Value);
        return true;
    }

    private void DropCarried(EntityUid carrier, EntityUid target)
    {
        OnCarryDropped(carrier, target);

        if (Exists(target) && !Deleted(target) && !Terminating(target))
        {
            RemComp<KnockedDownComponent>(target);
            RemComp<ActiveCanBeCarriedComponent>(target);
            _actionBlocker.UpdateCanMove(target);
            if (!Terminating(target))
                _transform.AttachToGridOrMap(target);
            _standing.Stand(target);

            var ev = new CarryDroppedEvent();
            RaiseLocalEvent(target, ref ev);
        }

        RemComp<ActiveCarrierComponent>(carrier);
        RemComp<CarryingSlowdownComponent>(carrier);

        _virtualItem.DeleteInHandsMatching(carrier, target);
        _movementSpeed.RefreshMovementSpeedModifiers(carrier);
    }

    #endregion

    #region Other APIs

    /// <summary>
    /// Called after carrying state and transform are fully initialized.
    /// </summary>
    /// <param name="carrier">Entity that started carrying.</param>
    /// <param name="target">Entity that started being carried.</param>
    protected virtual void OnCarryStarted(EntityUid carrier, EntityUid target)
    {
    }

    /// <summary>
    /// Called before carrying state components are removed.
    /// </summary>
    /// <param name="carrier">Entity that was carrying the target.</param>
    /// <param name="target">Entity that is being dropped.</param>
    protected virtual void OnCarryDropped(EntityUid carrier, EntityUid target)
    {
    }

    /// <summary>
    /// Applies movement slowdown to the carrier based on carried entity and mass ratio.
    /// </summary>
    /// <param name="carrier">Entity that receives the slowdown.</param>
    /// <param name="carried">Entity being carried.</param>
    protected void ApplySlowdown(Entity<CarrierComponent> carrier, Entity<CanBeCarriedComponent> carried)
    {
        var massRatio = MassContest(carrier.Owner, carried.Owner);
        var mobState = TryComp<MobStateComponent>(carried, out var mobStateComp)
            ? mobStateComp.CurrentState
            : MobState.Invalid;

        var modifier = CalculateSpeedModifier(mobState, massRatio, carrier.Comp, carried.Comp);
        _slowdown.SetModifier(carrier.Owner, modifier, modifier);
    }

    /// <summary>
    /// Calculates the mass ratio between two entities.
    /// </summary>
    /// <param name="carrier">Entity whose mass is compared against the target.</param>
    /// <param name="target">Entity used as the mass comparison target.</param>
    /// <returns>Mass ratio of roller to target, or 1 if either mass cannot be read safely.</returns>
    protected float MassContest(Entity<PhysicsComponent?> carrier, Entity<PhysicsComponent?> target)
    {
        if (!Resolve(carrier, ref carrier.Comp, false) || !Resolve(target, ref target.Comp, false))
            return 1f;

        if (MathHelper.CloseTo(target.Comp.FixturesMass, 0f) || MathHelper.CloseTo(carrier.Comp.FixturesMass, 0f))
            return 1f;

        return carrier.Comp.FixturesMass / target.Comp.FixturesMass;
    }

    /// <summary>
    /// Calculates throw speed for a carried entity.
    /// </summary>
    /// <param name="baseThrowSpeed">Original throw speed from the throw action.</param>
    /// <param name="massRatio">Mass ratio between the carrier and carried entity.</param>
    /// <param name="carrier">Carrier configuration used for throw speed limits.</param>
    /// <returns>Final throw speed clamped to carrier limits.</returns>
    protected static float CalculateThrowSpeed(float baseThrowSpeed, float massRatio, CarrierComponent carrier)
    {
        var massModifier = MathF.Pow(MathF.Max(massRatio, 0f), carrier.ThrowMassExponent);
        var speed = baseThrowSpeed * carrier.ThrowSpeedModifier * massModifier;
        return Math.Clamp(speed, carrier.MinThrowSpeed, carrier.MaxThrowSpeed);
    }

    /// <summary>
    /// Calculates throw distance for a carried entity.
    /// </summary>
    /// <param name="throwSpeed">Final throw speed.</param>
    /// <param name="carrier">Carrier configuration used for throw distance limits.</param>
    /// <returns>Final throw distance clamped to carrier limits.</returns>
    protected static float CalculateThrowDistance(float throwSpeed, CarrierComponent carrier)
    {
        var distance = throwSpeed * throwSpeed / carrier.ThrowGravity;
        return Math.Clamp(distance, carrier.MinThrowDistance, carrier.MaxThrowDistance);
    }

    /// <summary>
    /// Calculates the movement speed modifier applied while carrying an entity.
    /// </summary>
    /// <param name="mobState">Current mob state of the carried entity.</param>
    /// <param name="massRatio">Mass ratio between the carrier and carried entity.</param>
    /// <param name="carrier">Carrier configuration used for mass slowdown limits.</param>
    /// <param name="carried">Carried entity configuration used for base slowdown values.</param>
    /// <returns>Final movement speed modifier clamped to carrier limits.</returns>
    protected static float CalculateSpeedModifier(
        MobState mobState,
        float massRatio,
        CarrierComponent carrier,
        CanBeCarriedComponent carried)
    {
        var baseModifier = mobState is MobState.Critical or MobState.Dead
            ? carried.IncapacitatedCarrierSpeedModifier
            : carried.CarrierSpeedModifier;

        var massModifier = CalculateMassSpeedModifier(massRatio, carrier);
        var modifier = baseModifier * massModifier;

        return Math.Clamp(modifier, carrier.MinSpeedModifier, 1f);
    }

    private static float CalculateMassSpeedModifier(float massRatio, CarrierComponent carrier)
    {
        if (massRatio <= 0f)
            return carrier.MinMassSlowdownModifier;

        var modifier = 1f + MathF.Log2(massRatio) * carrier.MassSlowdownInfluence;
        return Math.Clamp(modifier, carrier.MinMassSlowdownModifier, carrier.MaxMassSlowdownModifier);
    }

    private void ShowCarryPopup(string locString, Filter filter, PopupType type, EntityUid carrier, EntityUid target)
    {
        var message = Loc.GetString(locString,
            ("carrier", Identity.Name(carrier, EntityManager)),
            ("target", Identity.Name(target, EntityManager)));

        _popup.PopupPredicted(message, carrier, target, filter, true, type);
    }

    #endregion
}
