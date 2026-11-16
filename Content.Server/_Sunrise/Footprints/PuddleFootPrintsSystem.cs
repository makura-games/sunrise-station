using System.Linq;
using Content.Shared._Sunrise.Footprints;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids.Components;
using Content.Shared.Standing;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Footprints;

/// <summary>
/// Handles footprint creation when entities interact with puddles
/// </summary>
public sealed partial class PuddleFootprintSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutionSystem = default!;
    [Dependency] private StandingStateSystem _standingStateSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PuddleFootprintComponent, EndCollideEvent>(OnPuddleInteraction);
    }

    /// <summary>
    /// Handles puddle interaction and footprint creation when entity exits the puddle
    /// </summary>
    private void OnPuddleInteraction(Entity<PuddleFootprintComponent> ent, ref EndCollideEvent args)
    {

        if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(args.OtherEntity))
            return;

        if (!TryComp<PuddleComponent>(ent, out var puddle)
            || !TryComp<FootprintEmitterComponent>(args.OtherEntity, out var emitter)
            || !_solutionSystem.ResolveSolution(ent.Owner, puddle.SolutionName, ref puddle.Solution, out var puddleSolutions))
            return;

        if (_gameTiming.CurTime < emitter.PuddleAbsorptionCooldownUntil)
            return;

        var stand = !_standingStateSystem.IsDown(args.OtherEntity);

        Solution solution;
        Entity<SolutionComponent> solComp;
        if (stand)
        {
            if (!_solutionSystem.ResolveSolution(args.OtherEntity, emitter.FootsSolutionName, ref emitter.FootsSolution, out var footsSolution))
                return;

            solution = footsSolution;
            solComp = emitter.FootsSolution.Value;
        }
        else
        {
            if (!_solutionSystem.ResolveSolution(args.OtherEntity, emitter.BodySurfaceSolutionName, ref emitter.BodySurfaceSolution, out var bodySurfaceSolution))
                return;

            solution = bodySurfaceSolution;
            solComp = emitter.BodySurfaceSolution.Value;
        }

        var totalSolutionQuantity = puddleSolutions.Contents.Sum(sol => (float)sol.Quantity);
        var waterQuantity = (from sol in puddleSolutions.Contents where sol.Reagent.Prototype == "Water" select (float)sol.Quantity).FirstOrDefault();

        if (waterQuantity / (totalSolutionQuantity / 100f) > ent.Comp.WaterThresholdPercent || puddleSolutions.Contents.Count <= 0)
            return;

        var availableSpace = solution.MaxVolume.Float() - solution.Volume.Float();

        if (availableSpace <= 0)
            return;

        var transferVolume = Math.Min(ent.Comp.TransferVolume, availableSpace);

        if (puddleSolutions.Volume < transferVolume)
            transferVolume = puddleSolutions.Volume.Float();

        if (transferVolume <= 0)
            return;

        var splitSolution = _solutionSystem.SplitSolution(puddle.Solution.Value, transferVolume);

        _solutionSystem.AddSolution(solComp, splitSolution);
    }
}
