using Content.Shared._Sunrise.Shipyard;
using Content.Shared._Sunrise.Shipyard.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Stacks;

namespace Content.Server._Sunrise.Shipyard;

public sealed partial class ShipyardSystem
{
    private static int GetPurchasePrice(ShipyardConsoleComponent console, int price)
    {
        return console.UseTickets ? (int) (((long) price + 1) / 2) : price;
    }

    private int GetTicketBalance(EntityUid station)
    {
        return TryComp<ShipyardTicketAccountComponent>(station, out var account) ? account.Balance : 0;
    }

    private bool TryAdjustTicketBalance(EntityUid station, int amount)
    {
        if (!Exists(station))
            return false;

        var account = EnsureComp<ShipyardTicketAccountComponent>(station);
        var balance = (long) account.Balance + amount;
        if (balance is < 0 or > int.MaxValue)
            return false;

        account.Balance = (int) balance;
        var query = EntityQueryEnumerator<ShipyardConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            if (console.UseTickets && _ui.IsUiOpen(uid, ShipyardConsoleUiKey.Key) &&
                _station.GetOwningStation(uid) == station)
                UpdateUi((uid, console));
        }

        return true;
    }

    public bool TryDepositTicket(Entity<ShipyardConsoleComponent?> ent, EntityUid ticket, EntityUid user)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !ent.Comp.UseTickets ||
            !TryComp<StackComponent>(ticket, out var stack) || stack.StackTypeId != "SalvageTicket" || stack.Unlimited)
            return false;

        Entity<ShipyardConsoleComponent> console = (ent.Owner, ent.Comp);
        if (!Exists(user) || !_interaction.InRangeAndAccessible((user, null), (ent.Owner, null)) ||
            !_access.IsAllowed(user, ent) || IsConsoleBroken(console) ||
            _station.GetOwningStation(ent) is not { } station)
            return false;

        var amount = _stack.GetCount((ticket, stack));
        if (amount <= 0 || !TryAdjustTicketBalance(station, amount))
            return false;

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} deposited {amount} salvage tickets into {ToPrettyString(ent):console}.");
        QueueDel(ticket);
        _audio.PlayPvs(ent.Comp.ConfirmSound, ent);
        _popup.PopupEntity(Loc.GetString("shipyard-console-ticket-deposit", ("amount", amount)), ent, user);
        return true;
    }
}
