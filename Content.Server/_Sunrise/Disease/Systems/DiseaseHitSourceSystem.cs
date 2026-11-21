using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Передаёт заразу ударом: от укуса зверя-переносчика до заражённого клинка в чужих руках.
/// </summary>
/// <remarks>
/// От брони удар защищает не здесь: шанс уже проходит через <c>DiseaseExposureAttemptEvent</c>,
/// который спрашивает надетые вещи, закрыто ли у цели нужное место. Поэтому укус через скафандр
/// не заражает, а проверять, сколько урона в итоге прошло, незачем.
/// </remarks>
public sealed partial class DiseaseHitSourceSystem : EntitySystem
{
    [Dependency] private DiseaseRegistrySystem _registry = default!;
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private IRobustRandom _random = default!;

    /// <summary>
    /// Заражает всех, кого задел удар.
    /// </summary>
    /// <remarks>
    /// Событие приходит на оружие, а при атаке без оружия оружие — сам атакующий,
    /// поэтому одна проверка покрывает и зверя, и клинок в руках.
    /// </remarks>
    [SubscribeLocalEvent]
    private void OnMeleeHit(Entity<DiseaseHitSourceComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.Weapon != ent.Owner || !CanExpose(ent))
            return;

        foreach (var target in args.HitEntities)
        {
            if (target == ent.Owner || target == args.User)
                continue;

            TryExpose(ent, target);
        }
    }

    /// <summary>Пытается заразить ударом, используя постоянный штамм источника.</summary>
    /// <remarks>
    /// Штамм берётся природный, общий на весь раунд: зверьё одного вида и однотипное оружие носят
    /// одну заразу, иначе стая мышей раздала бы экипажу десяток штаммов, и на каждый понадобилась бы
    /// своя вакцина. Выбор из списка делается один раз на источник.
    /// </remarks>
    public bool TryExpose(Entity<DiseaseHitSourceComponent> ent, EntityUid target)
    {
        if (!CanExpose(ent) || TerminatingOrDeleted(target) || target == ent.Owner)
            return false;

        if (ent.Comp.Strain is not { } existing || !_registry.StrainExists(existing))
            ent.Comp.Strain = _registry.GetNatural(_random.Pick(ent.Comp.Diseases));

        return ent.Comp.Strain is { } strain &&
               _disease.TryInfect(target, strain, DiseaseTransmissionRoute.Contact, ent.Owner, ent.Comp.Chance,
                   ent.Comp.Contact);
    }

    /// <summary>Может ли этот источник вообще кого-то заразить.</summary>
    public bool CanExpose(Entity<DiseaseHitSourceComponent> ent)
    {
        return !TerminatingOrDeleted(ent) && ent.Comp.Chance > 0f && ent.Comp.Diseases.Count > 0;
    }
}
