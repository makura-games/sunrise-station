using Content.Client._Sunrise.Shipyard.UI;
using Content.Shared._Sunrise.Shipyard;
using Content.Shared._Sunrise.Shipyard.BUI;
using Content.Shared._Sunrise.Shipyard.Events;
using Robust.Client.UserInterface;

namespace Content.Client._Sunrise.Shipyard.BUI;

public sealed class ShipyardConsoleBoundUserInterface : BoundUserInterface
{
    private ShipyardConsoleMenu? _menu;

    public ShipyardConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<ShipyardConsoleMenu>();
        _menu.OnPurchase += vesselId => SendMessage(new ShipyardConsolePurchaseMessage(vesselId));
        _menu.OnSell += () => SendMessage(new ShipyardConsoleSellMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ShipyardConsoleInterfaceState shipyardState)
            _menu?.UpdateState(shipyardState);
    }
}
