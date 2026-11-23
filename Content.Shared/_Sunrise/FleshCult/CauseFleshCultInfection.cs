using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.FleshCult;

public sealed partial class CauseFleshCultInfectionEntityEffectSystem : EntityEffectSystem<MobStateComponent, CauseFleshCultInfection>
{
    private readonly string _reagent = "UnstableMutagen";

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<CauseFleshCultInfection> args)
    {
        if (HasComp<MindShieldComponent>(entity))
        {
            // Inject into chemical solution (bloodstream) specifically, like hyposprays/syringes do
            if (TryComp<BloodstreamComponent>(entity, out var bloodstream))
            {
                var solutionContainerSystem = EntityManager.System<SharedSolutionContainerSystem>();
                if (solutionContainerSystem.ResolveSolution(entity.Owner, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var chemSolution))
                {
                    // Remove Carol reagent and replace with Unstable Mutagen if target has mindshield
                    chemSolution.RemoveReagent("Carol", FixedPoint2.New(5));
                    chemSolution.AddReagent(_reagent, FixedPoint2.New(5));
                }
            }
        }
        else
            EnsureComp<PendingFleshCultistComponent>(entity);

    }
}

public sealed partial class CauseFleshCultInfection : EntityEffectBase<CauseFleshCultInfection>
{
    [DataField]
    public bool Innoculate;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("reagent-effect-guidebook-cause-flesh-cultist-infection", ("chance", Probability));
    }
}
