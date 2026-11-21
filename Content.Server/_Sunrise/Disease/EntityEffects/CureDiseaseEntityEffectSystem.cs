using Content.Server._Sunrise.Disease.Components;
using Content.Server._Sunrise.Disease.Systems;
using Content.Shared._Sunrise.Disease.EntityEffects;
using Content.Shared.EntityEffects;

namespace Content.Server._Sunrise.Disease.EntityEffects;

/// <summary>
/// Обрабатывает <see cref="CureDisease"/>. Событие получают только заражённые носители.
/// </summary>
public sealed partial class CureDiseaseEntityEffectSystem : EntityEffectSystem<DiseaseCarrierComponent, CureDisease>
{
    [Dependency] private DiseaseSystem _disease = default!;

    protected override void Effect(Entity<DiseaseCarrierComponent> entity, ref EntityEffectEvent<CureDisease> args)
    {
        _disease.AddCureProgress(entity.AsNullable(),
            args.Effect.Potency * args.Scale,
            args.Effect.Diseases,
            args.Effect.Classification);
    }
}
