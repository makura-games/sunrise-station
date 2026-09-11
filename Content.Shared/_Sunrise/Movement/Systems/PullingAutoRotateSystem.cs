using Content.Shared.MouseRotator;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared._Sunrise.Movement.Systems;

/// <summary>
/// Rotates an active puller toward the entity being pulled.
/// </summary>
public sealed class PullingAutoRotateSystem : EntitySystem
{
    [Dependency] private readonly AutoRotateToTargetSystem _autoRotate = default!;

    private EntityQuery<TransformComponent> _transformQuery;

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(SharedMoverController));
        UpdatesAfter.Add(typeof(SharedMouseRotatorSystem));
        base.Initialize();

        _transformQuery = GetEntityQuery<TransformComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActivePullerComponent, PullerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var puller, out var transform))
        {
            if (puller.Pulling is not { } target ||
                !_transformQuery.TryComp(target, out var targetTransform))
            {
                continue;
            }

            _autoRotate.TryRotateToEntity(
                (uid, transform),
                (target, targetTransform),
                frameTime);
        }
    }
}
