using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Research.Artifact.Effects.ModifyThirst;

public sealed partial class ArtifactModifyThirstSystem : BaseXAESystem<ArtifactModifyThirstComponent>
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ThirstSystem _thirst = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly HashSet<Entity<ThirstComponent>> _entities = [];

    protected override void OnActivated(Entity<ArtifactModifyThirstComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        _entities.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.Range, _entities);

        foreach (var uid in _entities)
        {
            var modifier = _random.NextFloat(ent.Comp.MinModifier, ent.Comp.MaxModifier);
            _thirst.ModifyThirst(uid, uid, modifier * ent.Comp.Amount);
        }
    }
}
