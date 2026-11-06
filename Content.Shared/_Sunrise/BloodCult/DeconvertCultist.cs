using System.Threading;
using Content.Shared._Sunrise.BloodCult.Components;
using Content.Shared._Sunrise.BloodCult.Pentagram;
using Content.Shared.EntityEffects;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Shared._Sunrise.BloodCult;

public sealed partial class DeconvertCultistEntityEffectSystem : EntityEffectSystem<BloodCultistComponent, DeconvertCultist>
{
    [Dependency] private SharedStunSystem _sharedStunSystem = default!;
    [Dependency] private SharedPopupSystem _sharedPopupSystem = default!;
    [Dependency] private EntityManager _entityManager = default!;

    private readonly string _сultistTag = "Cultist";
    private readonly string _deconvertedTag = "DeconvertedCultist";

    protected override void Effect(Entity<BloodCultistComponent> entity, ref EntityEffectEvent<DeconvertCultist> args)
    {
        var uid = entity.Owner;

        if (entity.Comp.HolyConvertToken != null)
            return;

        var random = new System.Random();
        var convert = random.Next(1, 101) <= entity.Comp.HolyConvertChance;
        if (!convert)
            return;

        _sharedStunSystem.TryAddParalyzeDuration(uid, TimeSpan.FromSeconds(5f));
        var target = Identity.Name(uid, _entityManager);
        _sharedPopupSystem.PopupEntity(Loc.GetString("holy-water-started-converting", ("target", target)), uid);

        entity.Comp.HolyConvertToken = new CancellationTokenSource();
        Timer.Spawn(TimeSpan.FromSeconds(entity.Comp.HolyConvertTime),
            () => ConvertCultist(uid, _entityManager),
            entity.Comp.HolyConvertToken.Token);
    }

    private void ConvertCultist(EntityUid uid, IEntityManager entityManager)
    {
        if (!entityManager.TryGetComponent<BloodCultistComponent>(uid, out var cultist))
            return;

        cultist.HolyConvertToken = null;
        entityManager.RemoveComponent<BloodCultistComponent>(uid);
        if (entityManager.HasComponent<CultMemberComponent>(uid))
            entityManager.RemoveComponent<CultMemberComponent>(uid);
        entityManager.System<TagSystem>().RemoveTag(uid, _сultistTag);
        entityManager.System<TagSystem>().AddTag(uid, _deconvertedTag);
    }
}

public sealed partial class DeconvertCultist : EntityEffectBase<DeconvertCultist>
{
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return Loc.GetString("entity-effect-guidebook-cure-zombie-infection", ("chance", Probability));
    }
}
