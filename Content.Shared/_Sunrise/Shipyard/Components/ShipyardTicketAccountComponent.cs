namespace Content.Shared._Sunrise.Shipyard.Components;

/// <summary>
/// Ticket balance shared by the station's shipyard consoles.
/// </summary>
[RegisterComponent]
public sealed partial class ShipyardTicketAccountComponent : Component
{
    [DataField]
    public int Balance;
}
