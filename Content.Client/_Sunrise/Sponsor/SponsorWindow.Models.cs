namespace Content.Client._Sunrise.Sponsor;

public sealed partial class SponsorWindow
{
    private sealed record SponsorStoreEntry(
        SponsorStoreEntryKind Kind,
        string? ItemId,
        string? PackId,
        string Name,
        string Description,
        string? PreviewPrototype,
        string[] ContentPreviewPrototypes,
        int? AccessTier,
        bool RequiresTierAccess,
        bool AccessTierInherited,
        int Price,
        bool Owned,
        bool RequiresSpecialAccess,
        string DetailsText,
        int OwnedItemCount,
        int TotalItemCount)
    {
        public string Key => Kind == SponsorStoreEntryKind.Pack
            ? PackId ?? string.Empty
            : ItemId ?? string.Empty;
    }

    private enum SponsorStoreEntryKind : byte
    {
        Item,
        Pack,
    }

    private enum StoreCatalogCategory : byte
    {
        Items,
        Packs,
    }

    private enum StoreCatalogFilter : byte
    {
        All,
        Purchasable,
        Owned,
    }
}
