using Content.Shared._Sunrise.Disease.Events;
using Content.Shared.Forensics.Components;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Отмечает переболевших как доноров сыворотки: по ДНК, а не по сущности.
/// Кровь переболевшего остаётся пригодной, даже если сам донор уже недоступен.
/// </summary>
/// <remarks>
/// Своего состояния у системы нет: список ДНК лежит на самом штамме,
/// в <see cref="DiseaseDefinitionComponent.Donors"/>.
/// </remarks>
public sealed partial class DiseaseDonorRegistrySystem : EntitySystem
{
    [Dependency] private DiseaseSystem _disease = default!;
    [Dependency] private DiseaseRegistrySystem _registry = default!;

    [Dependency] private EntityQuery<DnaComponent> _dnaQuery = default!;

    [SubscribeLocalEvent]
    private void OnCured(Entity<DnaComponent> ent, ref DiseaseCuredEvent args)
    {
        // В реестр попадают только настоящие переболевшие: админская отмена болезни иммунитета не даёт.
        if (!_disease.HasImmunity(ent, args.Disease))
            return;

        RegisterDonor(ent.AsNullable(), args.Disease);
    }

    /// <summary>
    /// Записывает донора как источник сыворотки против болезни.
    /// </summary>
    public void RegisterDonor(Entity<DnaComponent?> ent, EntityUid disease)
    {
        if (!_dnaQuery.Resolve(ent, ref ent.Comp, false) || ent.Comp.DNA is not { } dna)
            return;

        if (_registry.TryGetData(disease, out var data))
            data.Donors.Add(dna);
    }

    /// <summary>
    /// Возвращает болезни, против которых годится кровь с этой ДНК.
    /// </summary>
    /// <remarks>
    /// Обход всех штаммов раунда: их десяток, а запрос идёт только при сборке состояния вакцинатора.
    /// </remarks>
    public List<EntityUid> GetDonorDiseases(string dna)
    {
        var diseases = new List<EntityUid>();
        foreach (var disease in _registry.GetDiseases())
        {
            if (_registry.TryGetData(disease, out var data) && data.Donors.Contains(dna))
                diseases.Add(disease);
        }

        return diseases;
    }
}
