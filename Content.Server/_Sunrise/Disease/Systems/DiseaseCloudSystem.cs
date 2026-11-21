using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Заражает тех, кто дышит заразным аэрозолем.
/// </summary>
/// <remarks>
/// Облако — обычная сущность с временем жизни, поэтому отдельная уборка ему не нужна:
/// как только истечёт <c>TimedDespawn</c>, исчезнет и источник заражения.
/// </remarks>
public sealed partial class DiseaseCloudSystem : EntitySystem
{
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedInternalsSystem _internals = default!;

    private readonly HashSet<Entity<MobStateComponent>> _targets = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<DiseaseCloudComponent>();
        while (query.MoveNext(out var uid, out var cloud))
        {
            if (cloud.Disease is not { } disease || cloud.NextInfect > curTime)
                continue;

            cloud.NextInfect = curTime + cloud.Interval;
            Expose((uid, cloud), disease);
        }
    }

    /// <summary>
    /// Подвергает действию облака всех, кто в него попал.
    /// </summary>
    private void Expose(Entity<DiseaseCloudComponent> ent, EntityUid disease)
    {
        _targets.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.Range, _targets, LookupFlags.Uncontained);

        foreach (var target in _targets)
        {
            // Стены и шлюзы держат аэрозоль так же, как капли от кашля.
            if (!_interaction.InRangeUnobstructed(ent.Owner, target.Owner, ent.Comp.Range))
                continue;

            // Баллон спасает лёгкие, но взвесь всё равно оседает на коже и глазах.
            var chance = ent.Comp.Chance;
            if (TryComp<InternalsComponent>(target, out var internals) && _internals.AreInternalsWorking(internals))
                chance *= ent.Comp.InternalsCoefficient;

            _disease.TryInfect(target, disease, DiseaseTransmissionRoute.Droplet, ent, chance,
                ignoreRoutes: ent.Comp.IgnoreRoutes);
        }
    }
}
