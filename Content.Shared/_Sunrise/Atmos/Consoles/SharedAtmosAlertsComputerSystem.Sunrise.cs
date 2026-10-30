using Content.Shared.Atmos.Components;
using Content.Shared.Verbs;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Atmos.Consoles;

public abstract partial class SharedAtmosAlertsComputerSystem
{
    private void InitializeSunrise()
    {
        SubscribeLocalEvent<AtmosAlertsComputerComponent, AtmosAlertsComputerAlertSoundToggleMessage>(OnAlertSoundToggleMessage);
        SubscribeLocalEvent<AtmosAlertsComputerComponent, GetVerbsEvent<InteractionVerb>>(AddToggleVerb);
    }

    private void OnAlertSoundToggleMessage(Entity<AtmosAlertsComputerComponent> ent, ref AtmosAlertsComputerAlertSoundToggleMessage args)
    {
        ent.Comp.DoAtmosAlert = args.Enabled;
        Dirty(ent);
    }

    private void AddToggleVerb(Entity<AtmosAlertsComputerComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var verb = new InteractionVerb
        {
            Text = Loc.GetString(ent.Comp.DoAtmosAlert
                ? "item-toggle-deactivate-alert"
                : "item-toggle-activate-alert"),
            Act = () => ToggleAlert(ent),
        };

        args.Verbs.Add(verb);
    }

    private void ToggleAlert(Entity<AtmosAlertsComputerComponent> ent)
    {
        ent.Comp.DoAtmosAlert = !ent.Comp.DoAtmosAlert;
        Dirty(ent);
    }
}
