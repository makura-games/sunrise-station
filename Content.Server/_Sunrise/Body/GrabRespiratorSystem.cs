using Content.Server.Body.Systems;
using Content.Shared._Sunrise.Grab;
using Content.Shared._Sunrise.Grab.Components;

namespace Content.Server._Sunrise.Body;

/// <summary>
/// Bridges suffocating grabs into the respirator breathing permission event.
/// </summary>
public sealed class GrabRespiratorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GrabbedComponent, CanBreatheEvent>(OnCanBreathe);
    }

    private void OnCanBreathe(Entity<GrabbedComponent> ent, ref CanBreatheEvent args)
    {
        if (ent.Comp.Stage >= GrabStage.Suffocate)
            args.Cancelled = true;
    }
}
