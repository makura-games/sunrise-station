using Content.Shared.Alert;
using Content.Shared._Sunrise.Tutorial.Components;

namespace Content.Shared._Sunrise.Tutorial.EntitySystems;

/// <summary>
/// Управляет жизненным циклом учебного алерта, добавленного эффектом шага туториала.
/// </summary>
public sealed partial class TutorialSimulatedAlertSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TutorialSimulatedAlertComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TutorialSimulatedAlertComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<TutorialSimulatedAlertComponent> ent, ref ComponentStartup args)
    {
        _alerts.ShowAlert(ent.Owner, ent.Comp.Alert, ent.Comp.Severity);
    }

    private void OnShutdown(Entity<TutorialSimulatedAlertComponent> ent, ref ComponentShutdown args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.Alert);
    }
}
