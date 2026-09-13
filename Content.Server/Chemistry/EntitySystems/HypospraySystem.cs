using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Events;
using Content.Shared.Clothing.EntitySystems;

namespace Content.Server.Chemistry.EntitySystems;

/// <summary>
/// Server-side system for hypospray that adds borg announcement functionality
/// </summary>
public sealed partial class ServerHypospraySystem : EntitySystem
{
    [Dependency] private Content.Server._Sunrise.Medical.BorgHypospraySystem _borgHypospray = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainers = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Subscribe to injection events after the shared system processes them
        SubscribeLocalEvent<InjectorComponent, BeforeInjectTargetEvent>(OnAfterInject);
    }

    private void OnAfterInject(Entity<InjectorComponent> entity, ref BeforeInjectTargetEvent args)
    {
        // Get the solution that was injected
        if (_solutionContainers.TryGetSolution(entity.Owner, entity.Comp.SolutionName, out var hypoSpraySoln, out _))
        {
            _borgHypospray.TryAnnounceInjection(entity.Owner, args.EntityUsingInjector, args.TargetGettingInjected, hypoSpraySoln.Value);
        }
    }
}
