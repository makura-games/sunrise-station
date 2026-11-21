using Content.Shared._Sunrise.Disease.Components;
using Content.Shared._Sunrise.Disease.Database;
using Content.Shared._Sunrise.Disease.Vaccinator;
using Robust.Shared.GameStates;

namespace Content.Client._Sunrise.Disease;

/// <summary>
/// Перерисовывает окна машин болезней, когда приходит новое состояние их компонентов.
/// </summary>
/// <remarks>
/// Данные панелей лежат в состоянии компонента, а не в состоянии интерфейса, поэтому окно
/// обновляется прямо по сетевому состоянию машины — без отдельной посылки от сервера.
/// </remarks>
public sealed partial class DiseaseConsoleSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    [SubscribeLocalEvent]
    private void OnVaccinatorState(Entity<VaccinatorComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateUi(ent.Owner, VaccinatorUiKey.Key);
    }

    [SubscribeLocalEvent]
    private void OnDatabaseState(Entity<DiseaseDatabaseConsoleComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateUi(ent.Owner, DiseaseDatabaseUiKey.Key);
    }

    private void UpdateUi(EntityUid uid, Enum key)
    {
        if (_ui.TryGetOpenUi(uid, key, out var bui))
            bui.Update();
    }
}
