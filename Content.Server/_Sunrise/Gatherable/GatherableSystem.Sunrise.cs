using Content.Server.Gatherable.Components;
using Robust.Shared.Random;

namespace Content.Server.Gatherable;

public sealed partial class GatherableSystem
{
    partial void RollGatherProjectileChance(Entity<GatheringProjectileComponent> gathering, ref bool canGather) =>
        canGather = _random.Prob(gathering.Comp.Chance);
}
