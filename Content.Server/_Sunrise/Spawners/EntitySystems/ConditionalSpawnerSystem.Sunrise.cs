using Content.Shared.Tag;
using Robust.Shared.Prototypes;

#pragma warning disable IDE0130 // Пространство имён vanilla-системы сохраняется для partial-расширения.
namespace Content.Server.Spawners.EntitySystems;

public sealed partial class ConditionalSpawnerSystem
{
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> StorytellerIgnoreMessTag = "StorytellerIgnoreMess";

    private void PropagateStorytellerIgnoreMess(EntityUid spawner, EntityUid spawned)
    {
        if (_tag.HasTag(spawner, StorytellerIgnoreMessTag))
            _tag.AddTag(spawned, StorytellerIgnoreMessTag);
    }
}
