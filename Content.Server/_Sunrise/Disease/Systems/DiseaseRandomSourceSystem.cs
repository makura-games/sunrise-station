using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Однократно разыгрывает заразу источника по списку кандидатов его прототипа.
/// </summary>
public sealed partial class DiseaseRandomSourceSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DiseaseRegistrySystem _registry = default!;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<DiseaseRandomSourceComponent> ent, ref MapInitEvent args)
    {
        // Бросок сделан: компонент снимается, чтобы на предмете не оставалось отработавшего генератора.
        RemCompDeferred<DiseaseRandomSourceComponent>(ent);

        var candidates = new List<ProtoId<DiseasePrototype>>();
        foreach (var template in ent.Comp.Templates)
        {
            if (_prototype.HasIndex(template) && !candidates.Contains(template))
                candidates.Add(template);
        }

        var count = Math.Clamp(ent.Comp.Count, 0, candidates.Count);
        if (count == 0)
            return;

        // Каждому предмету заводится собственный штамм: две одинаковые посылки за раунд
        // должны требовать двух разных вакцин, а не закрываться первой же.
        var picked = new List<EntityUid>(count);
        for (var i = 0; i < count; i++)
        {
            var index = _random.Next(candidates.Count);
            if (_registry.CreateStrain(candidates[index]) is { } strain)
                picked.Add(strain);

            candidates.RemoveAt(index);
        }

        if (picked.Count == 0)
            return;

        // Остальные настройки источника уже стоят в прототипе: здесь решается только, чем он заражён.
        var source = EnsureComp<DiseaseSourceComponent>(ent);
        source.Templates = new List<ProtoId<DiseasePrototype>>();
        source.Diseases = picked;
    }
}
