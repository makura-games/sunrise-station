using Content.Server._Sunrise.Disease.Components;
using Content.Server._Sunrise.Disease.Systems;
using Content.Shared._Sunrise.Disease.EntityEffects;
using Content.Shared.EntityEffects;

namespace Content.Server._Sunrise.Disease.EntityEffects;

/// <summary>
/// Обрабатывает <see cref="RelieveSymptoms"/>. Событие получают только заражённые носители.
/// </summary>
public sealed partial class RelieveSymptomsEntityEffectSystem : EntityEffectSystem<DiseaseCarrierComponent, RelieveSymptoms>
{
    [Dependency] private DiseaseSystem _disease = default!;

    protected override void Effect(Entity<DiseaseCarrierComponent> entity, ref EntityEffectEvent<RelieveSymptoms> args)
    {
        _disease.TryRelieve(entity.AsNullable(),
            args.Effect.Duration * args.Scale,
            args.Effect.MaxDuration,
            args.Effect.Diseases,
            args.Effect.Classification);
    }
}
