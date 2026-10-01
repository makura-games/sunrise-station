using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Пространство имён vanilla-системы сохраняется для partial-расширения.
namespace Content.Server.Spawners.EntitySystems;

public sealed partial class ConditionalSpawnerSystem
{
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> StorytellerIgnoreMessTag = "StorytellerIgnoreMess";

    private EntityUid SpawnWithStorytellerPropagation(EntityUid spawner,
        EntProtoId prototype,
        MapCoordinates coordinates,
        Angle rotation)
    {
        var spawned = Spawn(prototype, coordinates, rotation: rotation);
        PropagateStorytellerIgnoreMess(spawner, spawned);
        return spawned;
    }

    private void SpawnStacksWithStorytellerPropagation(EntityUid spawner,
        EntProtoId prototype,
        int count,
        EntityCoordinates coordinates)
    {
        var spawned = _stack.SpawnMultipleAtPosition(prototype, count, coordinates);
        foreach (var entity in spawned)
        {
            PropagateStorytellerIgnoreMess(spawner, entity);
        }
    }

    private void PropagateStorytellerIgnoreMess(EntityUid spawner, EntityUid spawned)
    {
        if (_tag.HasTag(spawner, StorytellerIgnoreMessTag))
            _tag.AddTag(spawned, StorytellerIgnoreMessTag);
    }
}
