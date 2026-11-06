using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Trigger;
using JetBrains.Annotations;

namespace Content.Server._Sunrise.SplashOnTrigger;

[UsedImplicitly]
public sealed partial class SplashOnTriggerSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private PuddleSystem _puddleSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SplashOnTriggerComponent, TriggerEvent>(OnSplashTrigger);
    }

    private void OnSplashTrigger(EntityUid uid, SplashOnTriggerComponent component, TriggerEvent args)
    {
        var xform = Transform(uid);

        var coords = xform.Coordinates;

        if (!coords.IsValid(EntityManager))
            return;

        var transferSolution = new Solution();
        foreach (var solution in component.SplashReagents)
        {
            transferSolution.AddReagent(solution.Reagent, solution.Quantity);
        }

        if (_solutionSystem.TryGetInjectableSolution(uid, out var injectableSolution, out _))
        {
            _solutionSystem.TryAddSolution(injectableSolution.Value, transferSolution);
        }

        _puddleSystem.TrySplashSpillAt(uid, coords, transferSolution, out var puddleUid);
    }
}
