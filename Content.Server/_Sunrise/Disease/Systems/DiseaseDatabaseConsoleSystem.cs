using Content.Server.Administration.Logs;
using Content.Server._Sunrise.Disease.Events;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Database;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared.Database;
using Content.Shared.Labels.EntitySystems;
using Robust.Server.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Консоль базы данных вирусов: показывает всё, что станция знает о штаммах и как далеко
/// продвинулось их исследование, и выдаёт лабораторные образцы штаммов в пробирках.
/// </summary>
/// <remarks>
/// Пробирка нужна, чтобы растить культуры без пациентов. Заразить ей нельзя: это образец,
/// а не источник инфекции.
/// </remarks>
public sealed partial class DiseaseDatabaseConsoleSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private DiseaseAnalysisSystem _analysis = default!;
    [Dependency] private DiseaseResearchSystem _research = default!;
    [Dependency] private DiseaseRegistrySystem _registry = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private LabelSystem _label = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<DiseaseDatabaseConsoleComponent>(DiseaseDatabaseUiKey.Key, subs =>
        {
            subs.Event<DiseaseDatabaseDispenseMessage>(OnDispense);
        });
    }

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<DiseaseDatabaseConsoleComponent> ent, ref MapInitEvent args)
    {
        UpdateState(ent);
    }

    [SubscribeLocalEvent]
    private void OnResearchChanged(ref DiseaseResearchChangedEvent args)
    {
        var query = EntityQueryEnumerator<DiseaseDatabaseConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            UpdateState((uid, console));
        }
    }

    private void OnDispense(Entity<DiseaseDatabaseConsoleComponent> ent, ref DiseaseDatabaseDispenseMessage args)
    {
        TryDispenseVial(ent, args.Index, args.Actor);
    }

    /// <summary>
    /// Выдаёт пробирку с лабораторным образцом выбранного штамма.
    /// </summary>
    /// <returns>True, если пробирка выдана.</returns>
    public bool TryDispenseVial(Entity<DiseaseDatabaseConsoleComponent> ent, int index, EntityUid user)
    {
        if (!CanDispense(ent))
            return false;

        var known = _research.GetKnownDiseases();
        if (index < 0 || index >= known.Count)
            return false;

        DispenseVial(ent, known[index], user);
        return true;
    }

    /// <summary>
    /// Готова ли консоль выдать пробирку.
    /// </summary>
    public bool CanDispense(Entity<DiseaseDatabaseConsoleComponent> ent)
    {
        return ent.Comp.NextDispense <= _timing.CurTime;
    }

    private void DispenseVial(Entity<DiseaseDatabaseConsoleComponent> ent, EntityUid disease, EntityUid user)
    {
        var strainCode = _analysis.GetStrainCode(disease);

        var vial = Spawn(ent.Comp.VialPrototype, Transform(ent).Coordinates);
        if (TryComp<DiseaseStrainSampleComponent>(vial, out var strain))
        {
            strain.Disease = disease;
            strain.HasStrain = true;
            strain.IsBlank = false;
            strain.IsDesigned = _registry.TryGetData(disease, out var definition) && definition.Designed;
            strain.StrainCode = strainCode;
            Dirty(vial, strain);
        }

        _label.Label(vial, strainCode);
        _audio.PlayPvs(ent.Comp.DispenseSound, ent.Owner);

        ent.Comp.NextDispense = _timing.CurTime + ent.Comp.DispenseDelay;

        _adminLog.Add(LogType.Disease, LogImpact.Low,
            $"{ToPrettyString(user):user} took a lab sample of {ToPrettyString(disease)} ({strainCode}) from {ToPrettyString(ent.Owner):console}");

        UpdateState(ent);
    }

    /// <summary>
    /// Переписывает записи базы в компонент и помечает его изменённым.
    /// </summary>
    /// <remarks>
    /// Данные едут состоянием компонента: окно рисуется из того, что у клиента уже есть,
    /// а готовность синтезатора он считает сам по <see cref="DiseaseDatabaseConsoleComponent.NextDispense"/>.
    /// </remarks>
    private void UpdateState(Entity<DiseaseDatabaseConsoleComponent> ent)
    {
        // Порядок записей совпадает с порядком штаммов в деле: по нему интерфейс и адресует выдачу.
        var known = _research.GetKnownDiseases();
        var entries = new List<DiseaseDatabaseEntry>(known.Count);
        foreach (var disease in known)
        {
            if (_research.GetDatabaseEntry(disease) is { } entry)
                entries.Add(entry);
        }

        ent.Comp.Entries = entries;
        Dirty(ent);
    }
}
