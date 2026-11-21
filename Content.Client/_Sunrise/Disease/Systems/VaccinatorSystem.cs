using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Systems;
using Content.Shared._Sunrise.Disease.Vaccinator;

namespace Content.Client._Sunrise.Disease.Systems;

/// <summary>
/// Клиентская часть вакцинатора: перерисовывает окно сразу после предсказанного запуска.
/// Реплицированные состояния перерисовывает <see cref="DiseaseConsoleSystem"/>.
/// </summary>
public sealed partial class VaccinatorSystem : SharedVaccinatorSystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    protected override void UpdateUi(Entity<VaccinatorComponent> ent)
    {
        if (_ui.TryGetOpenUi(ent.Owner, VaccinatorUiKey.Key, out var bui))
            bui.Update();
    }
}
