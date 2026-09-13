using Content.Shared.Whitelist;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;

namespace Content.Server._Sunrise.Research.Artifact.Effects.AddComponentsInRadius;

public sealed partial class AddComponentsInRadiusSystem : BaseXAESystem<AddComponentsInRadiusComponent>
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private readonly HashSet<Entity<TransformComponent>> _entities = [];

    protected override void OnActivated(Entity<AddComponentsInRadiusComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        var coords = Transform(ent).Coordinates;

        _entities.Clear();
        _lookup.GetEntitiesInRange(coords, ent.Comp.Radius, _entities, ent.Comp.SearchFlags);

        foreach (var target in _entities)
        {
            if (!_whitelist.CheckBoth(target, ent.Comp.Blacklist, ent.Comp.Whitelist))
                continue;

            EntityManager.AddComponents(target, ent.Comp.Components, ent.Comp.RemoveExistingComponents);
        }
    }
}
