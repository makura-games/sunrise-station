using Content.Server._Sunrise.Disease.Components;
using Content.Server._Sunrise.Disease.Systems;
using Content.Shared._Sunrise.Disease.EntityEffects;
using Content.Shared.EntityEffects;

namespace Content.Server._Sunrise.Disease.EntityEffects;

/// <summary>
/// Обрабатывает <see cref="SuppressDisease"/>. Событие получают только заражённые носители.
/// </summary>
public sealed partial class SuppressDiseaseEntityEffectSystem : EntityEffectSystem<DiseaseCarrierComponent, SuppressDisease>
{
    [Dependency] private DiseaseSystem _disease = default!;

    protected override void Effect(Entity<DiseaseCarrierComponent> entity, ref EntityEffectEvent<SuppressDisease> args)
    {
        _disease.TrySuppress(entity.AsNullable(),
            args.Effect.Duration * args.Scale,
            args.Effect.MaxDuration,
            args.Effect.Diseases,
            args.Effect.Classification);
    }
}
