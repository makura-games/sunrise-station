using System.Collections.Generic;

namespace Content.Client._Sunrise.SponsorInventory;

/// <summary>
/// Identifies which profile layer an inventory palette entry mutates.
/// </summary>
internal enum InventoryPaletteEntrySource : byte
{
    Loadout,
    Sponsor,
}

/// <summary>
/// Immutable view model for a selectable loadout or sponsor inventory item in the palette.
/// </summary>
/// <param name="Id">Stable UI identity from the source collection. This is not an authorization token.</param>
/// <param name="GroupId">Loadout group id for loadout entries.</param>
/// <param name="LoadoutId">Loadout prototype id for loadout entries.</param>
/// <param name="SponsorItemId">Sponsor inventory item id for sponsor entries.</param>
internal sealed record InventoryPaletteEntry(
    InventoryPaletteEntrySource Source,
    string Id,
    string Name,
    string IconPrototype,
    string Description,
    string Requirements,
    HashSet<string> TargetSlots,
    List<string> BagPrototypes,
    bool CanPlaceInBag,
    string? GroupId,
    string? GroupName,
    int GroupOrder,
    int GroupSelectedCount,
    int GroupMinLimit,
    int? GroupMaxLimit,
    string? LoadoutId,
    string? SponsorItemId,
    bool CanRemove,
    bool Enabled,
    bool Selected);
