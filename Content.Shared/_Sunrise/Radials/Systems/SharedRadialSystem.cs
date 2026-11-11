using Content.Shared.ActionBlocker;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory.VirtualItem;
using Robust.Shared.Containers;

namespace Content.Shared._Sunrise.Radials.Systems;

public abstract partial class SharedRadialSystem : EntitySystem
    {
        [Dependency] private SharedInteractionSystem _interactionSystem = default!;
        [Dependency] private ActionBlockerSystem _actionBlockerSystem = default!;
        [Dependency] protected SharedContainerSystem ContainerSystem = default!;
        [Dependency] private SharedHandsSystem _handsSystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeAllEvent<ExecuteRadialEvent>(HandleExecuteRadial);
        }

        private void HandleExecuteRadial(ExecuteRadialEvent args, EntitySessionEventArgs eventArgs)
        {
            var user = eventArgs.SenderSession.AttachedEntity;
            if (user == null)
                return;

            if (Deleted(GetEntity(args.Target)) || Deleted(user))
                return;

            var radials = GetLocalRadials(GetEntity(args.Target), user.Value, args.RequestedRadial.GetType());

            if (radials.TryGetValue(args.RequestedRadial, out var radial))
                ExecuteRadial(radial, user.Value, GetEntity(args.Target));
        }

        /// <summary>
        ///     Raises a number of events in order to get all verbs of the given type(s) defined in local systems. This
        ///     does not request verbs from the server.
        /// </summary>
        public SortedSet<Radial> GetLocalRadials(EntityUid target, EntityUid user, Type type, bool force = false)
        {
            return GetLocalRadials(target, user, new HashSet<Type>() { type }, force);
        }

        /// <summary>
        ///     Raises a number of events in order to get all verbs of the given type(s) defined in local systems. This
        ///     does not request verbs from the server.
        /// </summary>
        public SortedSet<Radial> GetLocalRadials(EntityUid target, EntityUid user, HashSet<Type> types, bool force = false)
        {
            SortedSet<Radial> radials = new();

            // accessibility checks
            bool canAccess = false;

            if (force || target == user)
                canAccess = true;

            else if (_interactionSystem.InRangeUnobstructed(user, target))
            {
                // Note that being in a container does not count as an obstruction for InRangeUnobstructed
                // Therefore, we need extra checks to ensure the item is actually accessible:
                if (ContainerSystem.IsInSameOrParentContainer(user, target))
                    canAccess = true;
                else
                    // the item might be in a backpack that the user has open
                    canAccess = _interactionSystem.CanAccessViaStorage(user, target);
            }

            // A large number of verbs need to check action blockers. Instead of repeatedly having each system individually
            // call ActionBlocker checks, just cache it for the verb request.
            var canInteract = force || _actionBlockerSystem.CanInteract(user, target);

            EntityUid? @using = null;
            if (TryComp(user, out HandsComponent? hands) && (force || _actionBlockerSystem.CanUseHeldEntity(user, target)))
            {
                @using = _handsSystem.GetActiveItem(user);

                // Check whether the "Held" entity is a virtual pull entity. If yes, set that as the entity being "Used".
                // This allows you to do things like buckle a dragged person onto a surgery table, without click-dragging
                // their sprite.

                if (TryComp(@using, out VirtualItemComponent? pull))
                    @using = pull.BlockingEntity;
            }

            // TODO: fix this garbage and use proper generics or reflection or something else, not this.
            if (types.Contains(typeof(InteractionRadial)))
            {
                var radialEvent = new GetRadialsEvent<InteractionRadial>(user, target, @using, hands, canInteract, canAccess);
                RaiseLocalEvent(target, radialEvent, true);
                radials.UnionWith(radialEvent.Radials);
            }

            // generic verbs
            if (types.Contains(typeof(Radial)))
            {
                var radialsEvent = new GetRadialsEvent<Radial>(user, target, @using, hands, canInteract, canAccess);
                RaiseLocalEvent(target, radialsEvent, true);
                radials.UnionWith(radialsEvent.Radials);
            }

            return radials;
        }

        public virtual void ExecuteRadial(Radial radial, EntityUid user, EntityUid target, bool forced = false)
        {
            // invoke any relevant actions
            radial.Act?.Invoke();

            // Maybe raise a local event
            if (radial.ExecutionEventArgs != null)
            {
                if (radial.EventTarget.IsValid())
                    RaiseLocalEvent(radial.EventTarget, radial.ExecutionEventArgs);
                else
                    RaiseLocalEvent(radial.ExecutionEventArgs);
            }

            if (Deleted(user) || Deleted(target))
                return;

            // Perform any contact interactions
            if (radial.DoContactInteraction ?? (radial.DefaultDoContactInteraction && _interactionSystem.InRangeUnobstructed(user, target)))
                _interactionSystem.DoContactInteraction(user, target);
        }
    }
