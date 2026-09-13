using System.Linq;
using Content.Shared._Sunrise.VentCraw;
using Content.Shared._Sunrise.VentCraw.Components;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Sunrise.VentCraw;

public sealed partial class VentCrawableSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private SharedTransformSystem _xformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VentCrawHolderComponent, VentCrawExitEvent>(OnVentCrawExitEvent);
    }

    /// <summary>
    /// Exits the vent craws for the specified VentCrawHolderComponent, removing it and any contained entities from the craws.
    /// </summary>
    /// <param name="uid">The EntityUid of the VentCrawHolderComponent.</param>
    /// <param name="holder">The VentCrawHolderComponent instance.</param>
    /// <param name="holderTransform">The TransformComponent instance for the VentCrawHolderComponent.</param>
    private void OnVentCrawExitEvent(EntityUid uid, VentCrawHolderComponent holder, ref VentCrawExitEvent args)
    {
        var holderTransform = args.holderTransform;

        if (Terminating(uid))
            return;

        if (!Resolve(uid, ref holderTransform))
            return;

        if (holder.IsExitingVentCraws)
        {
            Log.Error("Tried exiting VentCraws twice. This should never happen.");
            return;
        }

        holder.IsExitingVentCraws = true;

        foreach (var entity in holder.Container.ContainedEntities.ToArray())
        {
            RemComp<BeingVentCrawComponent>(entity);

            var meta = MetaData(entity);
            _containerSystem.Remove(entity, holder.Container, reparent: false, force: true);

            var xform = Transform(entity);
            if (xform.ParentUid != uid)
                continue;

            _xformSystem.AttachToGridOrMap(entity, xform);

            if (TryComp<VentCrawlerComponent>(entity, out var ventCrawComp))
            {
                ventCrawComp.InTube = false;
                Dirty(entity , ventCrawComp);
            }

            if (TryComp(entity, out PhysicsComponent? physics))
            {
                _physicsSystem.WakeBody(entity, body: physics);
            }
        }

        Del(uid);
    }
}
