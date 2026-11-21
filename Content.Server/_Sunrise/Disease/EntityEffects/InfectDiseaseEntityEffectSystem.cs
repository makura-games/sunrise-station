using Content.Server._Sunrise.Disease.Systems;
using Content.Shared._Sunrise.Disease.EntityEffects;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs.Components;

namespace Content.Server._Sunrise.Disease.EntityEffects;

/// <summary>
/// Обрабатывает <see cref="InfectDisease"/> через обычные проверки заражения.
/// </summary>
public sealed partial class InfectDiseaseEntityEffectSystem : EntityEffectSystem<MobStateComponent, InfectDisease>
{
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private DiseaseRegistrySystem _registry = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<InfectDisease> args)
    {
        if (_registry.GetNatural(args.Effect.Disease) is not { } disease)
            return;

        _disease.TryInfect(entity, disease, args.Effect.Route, args.User, args.Effect.Chance);
    }
}
