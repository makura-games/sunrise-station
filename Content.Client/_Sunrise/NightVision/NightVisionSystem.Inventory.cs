using Content.Shared.Inventory;

#pragma warning disable IDE0130 // Используем пространство имён расширяемой системы Wizden.
namespace Content.Client.NightVision;

public sealed partial class NightVisionSystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public override void Shutdown()
    {
        _overlayMan.RemoveOverlay(_overlay);
        _overlay.Dispose();
        base.Shutdown();
    }

    private EntityUid GetNightVisionViewer(EntityUid source)
    {
        if (_inventory.InSlotWithAnyFlags((source, null, null), SlotFlags.WITHOUT_POCKET))
            return Transform(source).ParentUid;

        return source;
    }
}
