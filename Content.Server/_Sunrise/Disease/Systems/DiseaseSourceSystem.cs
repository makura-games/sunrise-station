using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Nutrition;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Whitelist;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Связывает заражённые предметы с API заражения: съевший, тронувший или наступивший
/// на <see cref="DiseaseSourceComponent"/> подвергается действию его болезней.
/// </summary>
/// <remarks>
/// События поедания и контакта поднимаются из shared-кода, поэтому подписка намеренно только серверная.
/// </remarks>
public sealed partial class DiseaseSourceSystem : EntitySystem
{
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private DiseaseResidueSystem _residue = default!;
    [Dependency] private DiseaseRegistrySystem _registry = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private EntityQuery<DiseaseDigestionComponent> _digestionQuery = default!;

    [SubscribeLocalEvent]
    private void OnIngested(Entity<DiseaseSourceComponent> ent, ref IngestedEvent args)
    {
        TryExpose(ent, args.Target, DiseaseTransmissionRoute.Ingestion, ent.Comp.IngestionChance);
    }

    /// <summary>
    /// Насколько желудки едока сбивают шанс заразиться этой пищей.
    /// </summary>
    /// <remarks>
    /// Опирается на тот же список желудка, по которому игра решает, можно ли это вообще есть:
    /// кому сырое мясо — обычная еда, тот и личинок из него переварит с большей вероятностью.
    /// Желудков может быть несколько, поэтому берётся самый стойкий из подходящих.
    /// </remarks>
    /// <returns>Множитель шанса заражения. Единица — особое пищеварение тут не помогает.</returns>
    private float GetDigestionMultiplier(EntityUid eater, EntityUid food)
    {
        if (!_body.TryGetOrgansWithComponent<StomachComponent>(eater, out var stomachs))
            return 1f;

        var multiplier = 1f;
        foreach (var stomach in stomachs)
        {
            if (stomach.Comp.SpecialDigestible == null ||
                !_digestionQuery.TryComp(stomach.Owner, out var digestion) ||
                !_whitelist.IsWhitelistPass(stomach.Comp.SpecialDigestible, food))
            {
                continue;
            }

            multiplier = Math.Min(multiplier, Math.Max(digestion.Multiplier, 0f));
        }

        return multiplier;
    }

    [SubscribeLocalEvent]
    private void OnContact(Entity<DiseaseSourceComponent> ent, ref ContactInteractionEvent args)
    {
        TryExpose(ent, args.Other, DiseaseTransmissionRoute.Contact, ent.Comp.ContactChance);

        // Тот, кто взял очаг в перчатках, уносит заразу на них.
        if ((ent.Comp.Routes & DiseaseTransmissionRoute.Contact) == 0 || ent.Comp.ContactChance <= 0f)
            return;

        foreach (var disease in GetDiseases(ent))
        {
            _residue.TryContaminate(args.Other, disease, ent);
        }
    }

    /// <summary>
    /// Шаг по источнику срабатывает, только если источник вообще заражает при наступании.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnStepAttempt(Entity<DiseaseSourceComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (ent.Comp.StepChance > 0f)
            args.Continue = true;
    }

    [SubscribeLocalEvent]
    private void OnStepTriggered(Entity<DiseaseSourceComponent> ent, ref StepTriggeredOffEvent args)
    {
        TryExpose(ent, args.Tripper, DiseaseTransmissionRoute.Contact, ent.Comp.StepChance, DiseaseContactKind.Step);
    }

    /// <summary>
    /// Подвергает сущность действию всех болезней источника по указанному пути.
    /// </summary>
    /// <param name="contact">Чем произошёл контакт, если путь контактный.</param>
    /// <returns>True, если сущность заразилась хотя бы одной болезнью.</returns>
    public bool TryExpose(
        Entity<DiseaseSourceComponent> source,
        EntityUid target,
        DiseaseTransmissionRoute route,
        float chance,
        DiseaseContactKind contact = DiseaseContactKind.Touch)
    {
        if ((source.Comp.Routes & route) == 0 || chance <= 0f)
            return false;

        // Приспособленный желудок сбивает шанс только на пищевом пути: на остальные он не влияет.
        if (route == DiseaseTransmissionRoute.Ingestion)
            chance *= GetDigestionMultiplier(target, source);

        var infected = false;
        foreach (var disease in GetDiseases(source))
        {
            infected |= _disease.TryInfect(target, disease, route, source, chance, contact);
        }

        return infected;
    }

    /// <summary>
    /// Болезни источника: шаблоны из прототипа заводятся как болезни раунда при первом обращении.
    /// </summary>
    /// <remarks>
    /// Выведенный штамм кладётся прямо в <see cref="DiseaseSourceComponent.Diseases"/>,
    /// поэтому заражённый им предмет работает наравне с природными источниками.
    /// </remarks>
    public IReadOnlyList<EntityUid> GetDiseases(Entity<DiseaseSourceComponent> source)
    {
        foreach (var template in source.Comp.Templates)
        {
            if (_registry.GetNatural(template) is not { } disease || source.Comp.Diseases.Contains(disease))
                continue;

            source.Comp.Diseases.Add(disease);
        }

        source.Comp.Diseases.RemoveAll(disease => !_registry.StrainExists(disease));
        return source.Comp.Diseases;
    }
}
