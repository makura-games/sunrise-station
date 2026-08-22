using System.Linq;
using Content.Server.Doors.Systems;
using Content.Shared._Sunrise.Helpers;
using Content.Shared.Doors.Components;
using Content.Shared.Xenoarchaeology.Artifact;
using Content.Shared.Xenoarchaeology.Artifact.XAE;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Research.Artifact.Effects.BoltAirlocks;

public sealed partial class ArtifactBoltAirlocksSystem : BaseXAESystem<ArtifactBoltAirlocksComponent>
{
    [Dependency] private DoorSystem _door = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly HashSet<Entity<DoorBoltComponent>> _entities = [];

    protected override void OnActivated(Entity<ArtifactBoltAirlocksComponent> ent, ref XenoArtifactNodeActivatedEvent args)
    {
        var coords = Transform(ent).Coordinates;

        _entities.Clear();
        _lookup.GetEntitiesInRange(coords, ent.Comp.Range, _entities, LookupFlags.Static);

        var filteredDoors  = _entities.ToList()
            .ShuffleRobust(_random)
            .TakePercentage(ent.Comp.Chance);

        foreach (var door in filteredDoors )
        {
            _door.SetBoltsDown(door, true);
        }
    }
}
