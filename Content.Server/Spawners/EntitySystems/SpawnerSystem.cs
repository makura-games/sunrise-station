using Content.Server.Spawners.Components;
using Content.Shared.Tag; // Sunrise-Edit
using Robust.Shared.Prototypes; // Sunrise-Edit
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Spawners.EntitySystems;

public sealed partial class SpawnerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TagSystem _tag = default!; // Sunrise-Edit

    // Sunrise-Edit start
    private static readonly ProtoId<TagPrototype> StorytellerIgnoreMessTag = "StorytellerIgnoreMess";
    // Sunrise-Edit end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TimedSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<TimedSpawnerComponent>();
        while (query.MoveNext(out var uid, out var timedSpawner))
        {
            if (timedSpawner.NextFire > curTime)
                continue;

            OnTimerFired(uid, timedSpawner);

            timedSpawner.NextFire += timedSpawner.IntervalSeconds;
        }
    }

    private void OnMapInit(Entity<TimedSpawnerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextFire = _timing.CurTime + ent.Comp.IntervalSeconds;
    }

    private void OnTimerFired(EntityUid uid, TimedSpawnerComponent component)
    {
        if (!_random.Prob(component.Chance))
            return;

        var number = _random.Next(component.MinimumEntitiesSpawned, component.MaximumEntitiesSpawned);
        var coordinates = Transform(uid).Coordinates;

        // Sunrise-Edit start
        var hasIgnoreMess = _tag.HasTag(uid, StorytellerIgnoreMessTag);
        // Sunrise-Edit end

        for (var i = 0; i < number; i++)
        {
            var entity = _random.Pick(component.Prototypes);
            var spawned = SpawnAtPosition(entity, coordinates);
            // Sunrise-Edit start
            if (hasIgnoreMess)
                _tag.AddTag(spawned, StorytellerIgnoreMessTag);
            // Sunrise-Edit end
        }
    }
}
