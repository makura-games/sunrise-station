namespace Content.Client._Sunrise.SponsorInventory;

public sealed partial class InventoryWindow
{
    /*
     * Small UI state enums owned by the inventory window.
     */
    private enum InventoryTab : byte
    {
        Equipment,
        Pets,
    }

    private enum InventoryPaletteSourceFilter : byte
    {
        All,
        Loadout,
        Sponsor,
    }
}
