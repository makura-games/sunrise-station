using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Server.EntityEffects.Effects;

/// <summary>
/// Polymorphs this entity into another entity.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class PolymorphEntityEffectSystem : EntityEffectSystem<PolymorphableComponent, Shared.EntityEffects.Effects.Polymorph>
{
    [Dependency] private PolymorphSystem _polymorph = default!;

    // Sunrise edit start - откладываем полиморф, чтобы не менять хранилище компонентов во время EntityQueryEnumerator
    // Ожидаем полноценный фикс от Wizden
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeferredPolymorphEvent>(OnDeferredPolymorph);
    }

    private void OnDeferredPolymorph(DeferredPolymorphEvent args)
    {
        if (TerminatingOrDeleted(args.Target))
            return;

        if (MetaData(args.Target).EntityPaused)
            return;

        if (!HasComp<PolymorphableComponent>(args.Target))
            return;

        _polymorph.PolymorphEntity(args.Target, args.Prototype);
    }

    protected override void Effect(Entity<PolymorphableComponent> entity, ref EntityEffectEvent<Shared.EntityEffects.Effects.Polymorph> args)
    {
        QueueLocalEvent(new DeferredPolymorphEvent(entity, args.Effect.Prototype));
    }

    private sealed class DeferredPolymorphEvent(EntityUid target, ProtoId<PolymorphPrototype> prototype) : EntityEventArgs
    {
        public EntityUid Target { get; } = target;
        public ProtoId<PolymorphPrototype> Prototype { get; } = prototype;
    }

    // Sunrise edit end
}
