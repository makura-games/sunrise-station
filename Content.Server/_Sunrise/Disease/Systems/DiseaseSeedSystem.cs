using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Atmos.Rotting;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Заводит болезнь в раунд вместе с сущностью: заражает носителя при появлении
/// и решает, оказался ли предмет заражённым.
/// </summary>
/// <remarks>
/// Оба компонента одноразовые: отработав, они снимаются, чтобы не висеть на сущности весь раунд.
/// </remarks>
public sealed partial class DiseaseSeedSystem : EntitySystem
{
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private DiseaseRegistrySystem _registry = default!;
    [Dependency] private IRobustRandom _random = default!;

    /// <summary>
    /// Заражает носителя, который появился в раунде уже больным.
    /// </summary>
    /// <remarks>
    /// Подписка именно на запуск компонента, а не на <c>MapInitEvent</c>: черты навешиваются
    /// на игрока уже после создания сущности, и карта к этому моменту давно проинициализирована.
    /// </remarks>
    [SubscribeLocalEvent]
    private void OnStartInfected(Entity<DiseaseStartInfectedComponent> ent, ref ComponentStartup args)
    {
        // Шаблон заводится как болезнь раунда: все заражённые одной чертой болеют одним штаммом.
        if (TryPick(ent.Comp.Diseases, ent.Comp.Chance, out var template) &&
            _registry.GetNatural(template) is { } disease)
        {
            // Безусловное заражение: у отыгрышной простуды нет путей передачи,
            // и обычная проверка пути её бы не пропустила.
            _disease.Infect(ent.Owner, disease);
        }

        RemCompDeferred<DiseaseStartInfectedComponent>(ent);
    }

    /// <summary>
    /// Решает, завелась ли зараза в начавшем гнить теле.
    /// </summary>
    /// <remarks>
    /// Болезнь новая, а не та, от которой существо умерло: в разлагающейся плоти заводится своё.
    /// Дальше труп работает обычным очагом, и снимается он тоже обычной дезинфекцией.
    /// </remarks>
    [SubscribeLocalEvent]
    private void OnBeginRotting(Entity<DiseaseRotSourceComponent> ent, ref BeginRottingEvent args)
    {
        if (TryPick(ent.Comp.Diseases, ent.Comp.Chance, out var template))
        {
            var source = EnsureComp<DiseaseSourceComponent>(ent);
            source.Routes |= DiseaseTransmissionRoute.Contact;
            source.ContactChance = Math.Max(source.ContactChance, ent.Comp.ContactChance);

            // Тело могло быть очагом и раньше: новую заразу добавляем к прежней, а не затираем её.
            if (!source.Templates.Contains(template))
                source.Templates.Add(template);
        }

        RemCompDeferred<DiseaseRotSourceComponent>(ent);
    }

    /// <summary>
    /// Решает, оказался ли предмет заражённым.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnContamination(Entity<DiseaseContaminationComponent> ent, ref ComponentStartup args)
    {
        if (TryPick(ent.Comp.Diseases, ent.Comp.Chance, out var template))
        {
            var source = EnsureComp<DiseaseSourceComponent>(ent);
            source.Templates = new List<ProtoId<DiseasePrototype>> { template };
            source.Routes = ent.Comp.Routes;
            source.IngestionChance = ent.Comp.IngestionChance;
            source.ContactChance = ent.Comp.ContactChance;
        }

        RemCompDeferred<DiseaseContaminationComponent>(ent);
    }

    private bool TryPick(List<ProtoId<DiseasePrototype>> diseases, float chance, out ProtoId<DiseasePrototype> disease)
    {
        disease = default;

        if (diseases.Count == 0 || !_random.Prob(chance))
            return false;

        disease = _random.Pick(diseases);
        return true;
    }
}
