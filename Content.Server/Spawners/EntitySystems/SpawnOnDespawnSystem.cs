using Content.Server.Spawners.Components;
using Content.Shared.Tag; // Sunrise-Edit
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Server.Spawners.EntitySystems;

public sealed partial class SpawnOnDespawnSystem : EntitySystem
{
    [Dependency] private TagSystem _tag = default!; // Sunrise-Edit

    // Sunrise-Edit start
    private static readonly ProtoId<TagPrototype> StorytellerIgnoreMessTag = "StorytellerIgnoreMess";
    // Sunrise-Edit end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnOnDespawnComponent, TimedDespawnEvent>(OnDespawn);
    }

    private void OnDespawn(EntityUid uid, SpawnOnDespawnComponent comp, ref TimedDespawnEvent args)
    {
        if (!TryComp(uid, out TransformComponent? xform))
            return;

        var spawned = Spawn(comp.Prototype, xform.Coordinates);
        // Sunrise-Edit start
        if (_tag.HasTag(uid, StorytellerIgnoreMessTag))
            _tag.AddTag(spawned, StorytellerIgnoreMessTag);
        // Sunrise-Edit end
    }

    public void SetPrototype(Entity<SpawnOnDespawnComponent> entity, EntProtoId prototype)
    {
        entity.Comp.Prototype = prototype;
    }
}
