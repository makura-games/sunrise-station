using Content.Server._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Prototypes;
using Content.Shared._Sunrise.Disease.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Disease.Systems;

/// <summary>
/// Серверная часть чашек Петри: переносит штамм с мазка или пробирки и выращивает культуру.
/// </summary>
/// <remarks>
/// Второй путь исследования рядом с анализом больных: культура не показывает, как болезнь
/// выглядит у людей, зато её можно вырастить без пациента и сдать в диагностер.
/// </remarks>
public sealed partial class DiseaseCultureSystem : SharedDiseaseCultureSystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    [Dependency] private EntityQuery<DiseaseSampleComponent> _sampleQuery = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<DiseaseCultureComponent>();
        while (query.MoveNext(out var uid, out var culture))
        {
            if (!culture.Inoculated || culture.Ready || culture.ReadyAt > curTime)
                continue;

            Grow((uid, culture));
        }
    }

    protected override void OnInoculated(Entity<DiseaseCultureComponent> dish, EntityUid user, EntityUid used)
    {
        // Чистый мазок тоже засевает чашку: она просто не прорастёт.
        dish.Comp.Disease = TryGetStrain(used, out var disease) ? disease : null;
        dish.Comp.ReadyAt = _timing.CurTime + dish.Comp.GrowTime;
        dish.Comp.Ready = false;
        Dirty(dish);
    }

    /// <summary>
    /// Завершает рост: культура со штаммом становится образцом для анализа, чистая остаётся пустой.
    /// </summary>
    private void Grow(Entity<DiseaseCultureComponent> dish)
    {
        dish.Comp.Ready = true;
        dish.Comp.HasColonies = dish.Comp.Disease != null;
        Dirty(dish);

        // Пустая чашка тоже годится в диагностер: анализ честно покажет, что патогенов нет.
        var sample = EnsureComp<DiseaseSampleComponent>(dish);
        sample.Kind = DiseaseSampleKind.Culture;
        sample.Counted = false;
        sample.SubjectName = Loc.GetString("disease-culture-subject");
        sample.Entries.Clear();

        if (dish.Comp.Disease is { } disease)
            sample.Entries.Add(new DiseaseSampleEntry(disease, null, new List<ProtoId<DiseaseSymptomPrototype>>()));

        _appearance.SetData(dish, DiseaseCultureVisuals.State, dish.Comp.HasColonies
            ? DiseaseCultureState.Grown
            : DiseaseCultureState.Empty);
    }

    /// <summary>
    /// Достаёт штамм из предмета, которым засевают чашку.
    /// </summary>
    private bool TryGetStrain(EntityUid used, out EntityUid disease)
    {
        disease = default;

        if (TryComp<DiseaseStrainSampleComponent>(used, out var strain) && strain.Disease is { } strainDisease)
        {
            disease = strainDisease;
            return true;
        }

        // С мазка берётся первая найденная болезнь: чашка растит один штамм.
        if (_sampleQuery.TryComp(used, out var sample) && sample.Entries.Count > 0)
        {
            disease = sample.Entries[0].Disease;
            return true;
        }

        return false;
    }
}
