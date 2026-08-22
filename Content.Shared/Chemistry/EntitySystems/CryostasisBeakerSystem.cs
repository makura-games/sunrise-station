using Content.Shared.Chemistry.Components;

namespace Content.Shared.Chemistry.EntitySystems;

/// <summary>
/// System that prevents solutions in cryostasis beakers from being heated above room temperature.
/// </summary>
public sealed class CryostasisBeakerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CryostasisBeakerComponent, SolutionChangedEvent>(OnSolutionChanged);
    }

    private void OnSolutionChanged(Entity<CryostasisBeakerComponent> ent, ref SolutionChangedEvent args)
    {
        var solution = args.Solution.Comp.Solution;
        if (solution.Temperature > ent.Comp.MaxTemperature)
            solution.Temperature = ent.Comp.MaxTemperature;
    }
}
