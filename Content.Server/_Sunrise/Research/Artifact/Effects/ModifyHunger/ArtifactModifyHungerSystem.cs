using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Research.Artifact.Effects.ModifyHunger;

public sealed partial class ArtifactModifyHungerSystem : BaseXAESystem<ArtifactModifyHungerComponent>
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private HungerSystem _hunger = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly HashSet<Entity<HungerComponent>> _entities = [];

    protected override void OnActivated(Entity<ArtifactModifyHungerComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        _entities.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.Range, _entities);

        foreach (var uid in _entities)
        {
            var modifier = _random.NextFloat(ent.Comp.MinModifier, ent.Comp.MaxModifier);
            _hunger.ModifyHunger(uid, modifier * ent.Comp.Amount);
        }
    }
}
