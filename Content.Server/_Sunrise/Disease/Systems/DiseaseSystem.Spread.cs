using Content.Server._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;

namespace Content.Server._Sunrise.Disease.Systems;

public sealed partial class DiseaseSystem
{
    /*
     * Часть системы про распространение: передача от носителя к носителю.
     * Передача происходит только на конкретных событиях (сработавший симптом или контакт), без глобального поиска соседей.
     */

    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private DiseaseResidueSystem _residue = default!;

    private readonly HashSet<Entity<MobStateComponent>> _spreadTargets = new();

    private void InitializeSpread()
    {
    }

    [SubscribeLocalEvent]
    private void OnCarrierContact(Entity<DiseaseCarrierComponent> ent, ref ContactInteractionEvent args)
    {
        SpreadByContact(ent, args.Other);
    }

    /// <summary>
    /// Пробует передать все болезни носителя прикосновением.
    /// </summary>
    /// <remarks>
    /// Зараза заодно оседает на перчатках цели, даже если они её защитили.
    /// Удары носителя тут не обрабатываются: болезни берут только гуманоиды и подопытные
    /// обезьяны, а их кулаки заразу не переносят. Зверьё-переносчик и заражённое оружие
    /// заражают ударом сами, через <see cref="DiseaseHitSourceComponent"/>.
    /// </remarks>
    private void SpreadByContact(Entity<DiseaseCarrierComponent> ent, EntityUid target)
    {
        for (var i = 0; i < ent.Comp.Diseases.Count; i++)
        {
            var instance = ent.Comp.Diseases[i];
            if (!_registry.TryGetData(instance.Disease, out var proto) || instance.Stage >= proto.Stages.Count)
                continue;

            var chance = proto.Stages[instance.Stage].ContactSpreadChance
                         * GetSuppressedSpreadMultiplier(instance, proto);

            if (chance <= 0f)
                continue;

            TryInfect(target, instance.Disease, DiseaseTransmissionRoute.Contact, ent, chance);

            if ((proto.Routes & DiseaseTransmissionRoute.Contact) != 0)
                _residue.TryContaminate(target, instance.Disease, ent);
        }
    }

    /// <summary>
    /// Подвергает мобов рядом с носителем действию капель, выпущенных симптомом.
    /// </summary>
    private void SpreadDroplets(
        Entity<DiseaseCarrierComponent> ent,
        DiseaseInstance instance,
        DiseaseDefinitionComponent proto,
        DiseaseStage stage,
        DiseaseSpread spread)
    {
        var chance = spread.Chance * stage.SpreadMultiplier * GetSuppressedSpreadMultiplier(instance, proto);
        if (chance <= 0f || spread.Range <= 0f)
            return;

        _spreadTargets.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, spread.Range, _spreadTargets, LookupFlags.Uncontained);

        foreach (var target in _spreadTargets)
        {
            if (target.Owner == ent.Owner)
                continue;

            // Стены, окна и шлюзы задерживают капли.
            if (!_interaction.InRangeUnobstructed(ent.Owner, target.Owner, spread.Range))
                continue;

            TryInfect(target, instance.Disease, DiseaseTransmissionRoute.Droplet, ent, chance);
        }
    }

    private static float GetSuppressedSpreadMultiplier(DiseaseInstance instance, DiseaseDefinitionComponent proto)
    {
        return instance.Suppressed ? proto.SuppressedSpreadMultiplier : 1f;
    }
}
