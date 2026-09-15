using Content.Server.Destructible;
using Content.Shared._Sunrise.Shipyard;
using Content.Shared._Sunrise.Shipyard.Components;
using Content.Shared.Destructible;
using Content.Shared.Repairable;
using Content.Shared.UserInterface;

namespace Content.Server._Sunrise.Shipyard;

public sealed partial class ShipyardSystem
{
    private void OnUiOpenAttempt(
        Entity<ShipyardConsoleComponent> ent,
        ref ActivatableUIOpenAttemptEvent args)
    {
        if (!IsConsoleBroken(ent))
            return;

        args.Cancel();
        if (!args.Silent)
            _popup.PopupEntity(Loc.GetString("shipyard-console-broken"), ent, args.User);
    }

    private void OnBreakage(Entity<ShipyardConsoleComponent> ent, ref BreakageEventArgs args)
    {
        _appearance.SetData(ent, ShipyardConsoleVisuals.Broken, true);
        _ui.CloseUi(ent.Owner, ShipyardConsoleUiKey.Key);
    }

    private void OnRepaired(Entity<ShipyardConsoleComponent> ent, ref RepairedEvent args)
    {
        _appearance.SetData(ent, ShipyardConsoleVisuals.Broken, false);
    }

    private bool IsConsoleBroken(EntityUid uid)
    {
        return TryComp<DestructibleComponent>(uid, out var destructible) && destructible.IsBroken;
    }
}
