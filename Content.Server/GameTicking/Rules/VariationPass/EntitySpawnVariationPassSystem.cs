using Content.Server.GameTicking.Rules.VariationPass.Components;
using Content.Shared.Storage;
using Content.Shared.Tag; // Sunrise-Edit
using Robust.Shared.Prototypes; // Sunrise-Edit
using Robust.Shared.Random;

namespace Content.Server.GameTicking.Rules.VariationPass;

/// <inheritdoc cref="EntitySpawnVariationPassComponent"/>
public sealed partial class EntitySpawnVariationPassSystem : VariationPassSystem<EntitySpawnVariationPassComponent>
{
    // Sunrise-Edit start
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> StorytellerIgnoreMessTag = "StorytellerIgnoreMess";
    // Sunrise-Edit end

    protected override void ApplyVariation(Entity<EntitySpawnVariationPassComponent> ent, ref StationVariationPassEvent args)
    {
        var totalTiles = Stations.GetTileCount(args.Station.AsNullable());

        var dirtyMod = Random.NextGaussian(ent.Comp.TilesPerEntityAverage, ent.Comp.TilesPerEntityStdDev);
        var trashTiles = Math.Max((int) (totalTiles * (1 / dirtyMod)), 0);

        for (var i = 0; i < trashTiles; i++)
        {
            if (!TryFindRandomTileOnStation(args.Station, out _, out _, out var coords))
                continue;

            var ents = EntitySpawnCollection.GetSpawns(ent.Comp.Entities, Random);
            foreach (var spawn in ents)
            {
                var spawned = SpawnAtPosition(spawn, coords);
                _tag.AddTag(spawned, StorytellerIgnoreMessTag); // Sunrise-Edit
            }
        }
    }
}

