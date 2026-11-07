using System.Linq;
using Content.Client.Inventory;
using Content.Client.Lobby;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Inventory.Controls;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;

namespace Content.Client._Sunrise.SponsorInventory;

public sealed partial class InventoryWindow
{
    private const string DefaultSlotHighlightTexturePath = "slot_highlight";
    private const string SponsorSlotHighlightTexturePath = "/Textures/Interface/Clockwork/slot_highlight.png";
    private const string StorageSlotsTexturePath = "Slots/back";
    private const string StorageTemplateFallback = "template_small";
    private static readonly HashSet<string> PreviewUnderwearSlots = new()
    {
        "bra",
        "pants",
        "socks",
    };

    // Equipment slot layout and slot-selection behavior for the sponsor inventory window.
    private void RefreshEquipment()
    {
        ClearPreviewDummy();
        _slotControls.Clear();
        _slotLabels.Clear();
        _paletteEntries.Clear();
        _sponsorBagPreviewItems.Clear();

        InventoryHost.RemoveAllChildren();
        BagGridHost.RemoveAllChildren();
        ItemPalette.RemoveAllChildren();

        CharacterPreview.SetEntity(null);

        BagTitle.Text = Loc.GetString("sunrise-inventory-bag-title");
        BagCapacityLabel.Text = string.Empty;
        BagHint.Text = string.Empty;
        StatusLabel.Text = string.Empty;

        UpdateBagEditorControls(enabled: false);

        if (_characterProfile == null)
        {
            _bagEditorOpen = false;
            UpdateBagEditorControls(enabled: false);
            StatusLabel.Text = Loc.GetString("sunrise-inventory-no-profile");
            AddPaletteInfo("sunrise-inventory-no-profile");
            return;
        }

        var jobId = CurrentJobId;
        if (jobId == null || !_prototype.TryIndex<JobPrototype>(jobId, out var job))
        {
            _bagEditorOpen = false;
            UpdateBagEditorControls(enabled: false);
            StatusLabel.Text = Loc.GetString("sunrise-inventory-loadout-select-job");
            AddPaletteInfo("sunrise-inventory-loadout-select-job");
            return;
        }

        UpdateBagEditorControls(enabled: true);

        var roleLoadout = EnsureRoleLoadout(jobId);
        _previewDummy = UserInterfaceManager.GetUIController<LobbyUIController>()
            .LoadProfileEntity(_characterProfile, job, true);
        ApplySpeciesInventoryTemplateToPreview(_previewDummy.Value, job);

        // The preview entity is the canonical temporary model: slot checks, bag fitting, palette filtering, and rendering all use it.
        ApplySponsorSlotsToPreview(_previewDummy.Value, GetSponsorSelectionSnapshot());
        EnsureSelectedLoadoutStorage(_previewDummy.Value, roleLoadout);
        CharacterPreview.SetEntity(_previewDummy);
        SetPreviewRotation(_previewRotation);
        RefreshPreviewClothingVisibility();

        var displayedSlots = GetDisplayedSlots();
        if (_selectedEquipmentSlot != null && displayedSlots.All(s => s.Name != _selectedEquipmentSlot))
            _selectedEquipmentSlot = null;

        BuildPlacementOptions(displayedSlots);
        BuildInventoryLayout(displayedSlots);
        PackSponsorBagItems();
        BuildPaletteEntries(roleLoadout);
        RenderBagGrid();
        RefreshPalette();
    }

    private RoleLoadout EnsureRoleLoadout(string jobId)
    {
        var jobLoadoutId = LoadoutSystem.GetJobPrototype(jobId);
        var roleLoadout = GetRoleLoadout(jobLoadoutId);

        // SetDefault can add required loadouts, so write the normalized loadout back to the cloned profile before rendering.
        roleLoadout.SetDefault(_characterProfile, _player.LocalSession, _prototype);
        _characterProfile = _characterProfile?.WithLoadout(roleLoadout);
        return roleLoadout;
    }

    private RoleLoadout GetRoleLoadout(string jobLoadoutId)
    {
        if (_characterProfile != null && _characterProfile.Loadouts.TryGetValue(jobLoadoutId, out var existing))
            return existing.Clone();

        return new RoleLoadout(jobLoadoutId);
    }

    private void ApplySpeciesInventoryTemplateToPreview(EntityUid dummy, JobPrototype job)
    {
        if (_characterProfile == null)
            return;

        if (job.JobPreviewEntity != null || job.JobEntity != null)
            return;

        if (!_entManager.TryGetComponent<InventoryComponent>(dummy, out var inventory))
            return;

        if (!_prototype.TryIndex(_characterProfile.Species, out var species))
            return;

        if (!_prototype.TryIndex(species.Prototype, out var speciesPrototype))
            return;

        if (!speciesPrototype.TryGetComponent<InventoryComponent>(out var speciesInventory, _entManager.ComponentFactory))
            return;

        _inventory.SetTemplateId((dummy, inventory), speciesInventory.TemplateId);
    }

    private void RefreshPreviewClothingVisibility()
    {
        if (_previewDummy == null)
            return;

        if (!_entManager.TryGetComponent<SpriteComponent>(_previewDummy.Value, out var sprite))
            return;

        if (!_entManager.TryGetComponent<InventorySlotsComponent>(_previewDummy.Value, out var inventorySlots))
            return;

        var preview = _previewDummy.Value;
        var hideClothes = HideClothesButton.Pressed;
        var visible = !hideClothes;

        foreach (var (slot, revealedLayers) in inventorySlots.VisualLayerKeys)
        {
            if (hideClothes && PreviewUnderwearSlots.Contains(slot))
                continue;

            foreach (var layerKey in revealedLayers)
            {
                if (_sprite.LayerMapTryGet((preview, sprite), layerKey, out var layer, false))
                    _sprite.LayerSetVisible((preview, sprite), layer, visible);
            }
        }
    }

    private void UpdateHideClothesButtonText()
    {
        HideClothesButton.Text = Loc.GetString(HideClothesButton.Pressed
            ? "sunrise-inventory-show-clothes"
            : "sunrise-inventory-hide-clothes");
    }

    private List<SlotDefinition> GetDisplayedSlots()
    {
        if (_previewDummy == null)
            return [];

        if (!_inventory.TryGetSlots(_previewDummy.Value, out var slots))
            return [];

        return slots
            .Where(s => s.ShowInWindow)
            .OrderBy(s => GetLogicalSlotPosition(s).Y)
            .ThenBy(s => GetLogicalSlotPosition(s).X)
            .ToList();
    }

    private void BuildInventoryLayout(List<SlotDefinition> slots)
    {
        if (slots.Count == 0)
            return;

        var logicalPositions = slots.ToDictionary(s => s.Name, GetLogicalSlotPosition);
        var minPosition = new Vector2i(
            logicalPositions.Values.Min(p => p.X),
            logicalPositions.Values.Min(p => p.Y));
        var occupiedPositions = new HashSet<Vector2i>();

        var display = new InventoryDisplay
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        InventoryHost.AddChild(display);

        // Labels are cached by slot id because later palette/status messages only know the string slot name.
        var selection = GetSponsorSelectionSnapshot();
        foreach (var slot in slots)
        {
            var button = new SlotButton
            {
                ButtonTexturePath = GetSlotTexturePath(slot.TextureName),
                FullButtonTexturePath = slot.FullTextureName ?? StorageTemplateFallback,
                StorageTexturePath = StorageSlotsTexturePath,
                SlotName = slot.Name,
            };
            UpdateSlotHighlight(button, selection.SlotItems.ContainsKey(slot.Name), slot.Name == _selectedEquipmentSlot);

            var prototype = GetSlotPrototype(slot.Name);
            if (_previewDummy != null && _inventory.TryGetSlotEntity(_previewDummy.Value, slot.Name, out var entity))
                button.SetEntity(entity.Value);
            else
                button.SetEntity(null);

            var slotLabel = GetSlotLabel(slot);
            _slotLabels[slot.Name] = slotLabel;
            button.TooltipSupplier = _ => new Tooltip
            {
                Text = prototype == null
                    ? slotLabel
                    : Loc.GetString(
                        "sunrise-inventory-slot-tooltip",
                        ("slot", slotLabel),
                        ("item", GetEntityName(prototype, prototype))),
            };

            if (CanRemoveSlotItem(slot.Name))
                button.AddChild(CreateRemoveButton(() => RemoveSlotItem(slot.Name), "sunrise-inventory-slot-remove-tooltip"));

            button.Pressed += OnSlotPressed;
            display.AddButton(button, GetFreeInventoryPosition(logicalPositions[slot.Name] - minPosition, occupiedPositions));
            _slotControls[button] = slot.Name;
        }
    }

    private static Vector2i GetLogicalSlotPosition(SlotDefinition slot)
    {
        if (LogicalSlotPositions.TryGetValue(slot.Name, out var position))
            return position;

        if (TryGetPocketIndex(slot.Name, out var pocketIndex))
        {
            var pocketOffset = Math.Max(0, pocketIndex - 1);
            return new Vector2i(1 + pocketOffset % 3, 7 + pocketOffset / 3);
        }

        return slot.UIWindowPosition;
    }

    private static Vector2i GetFreeInventoryPosition(Vector2i position, HashSet<Vector2i> occupiedPositions)
    {
        if (occupiedPositions.Add(position))
            return position;

        // Custom slots can collide with upstream UIWindowPosition values; walk diagonally until a free display cell is found.
        for (var distance = 1; distance < 32; distance++)
        {
            for (var xOffset = 0; xOffset <= distance; xOffset++)
            {
                var candidate = new Vector2i(position.X + xOffset, position.Y + distance - xOffset);
                if (occupiedPositions.Add(candidate))
                    return candidate;
            }
        }

        return position;
    }

    private void OnSlotPressed(GUIBoundKeyEventArgs args, SlotControl control)
    {
        if (args.Function != EngineKeyFunctions.UIClick || !_slotControls.TryGetValue(control, out var slot))
            return;

        SelectEquipmentSlot(slot);
        args.Handle();
    }

    private void SelectEquipmentSlot(string slot)
    {
        var wasBagEditorOpen = _bagEditorOpen;
        _bagEditorOpen = false;
        _selectedEquipmentSlot = slot;

        // Selecting a slot also selects the matching placement filter so search results stay scoped to that slot.
        var placementIndex = _placementOptions.IndexOf(slot);
        if (placementIndex >= 0)
        {
            _placementFilter = placementIndex;
            PlacementFilter.SelectId(placementIndex);
        }

        if (wasBagEditorOpen)
        {
            RefreshEquipment();
            return;
        }

        RefreshSlotHighlights();
        RefreshPalette();
    }

    private void RefreshSlotHighlights()
    {
        var selection = GetSponsorSelectionSnapshot();
        foreach (var (control, slot) in _slotControls)
        {
            UpdateSlotHighlight(control, selection.SlotItems.ContainsKey(slot), slot == _selectedEquipmentSlot);
        }
    }

    private static void UpdateSlotHighlight(SlotControl control, bool hasSponsorItem, bool selected)
    {
        control.Highlight = hasSponsorItem || selected;
        control.HighlightRect.ModulateSelfOverride = null;
        control.HighlightTexturePath = hasSponsorItem && !selected
            ? SponsorSlotHighlightTexturePath
            : DefaultSlotHighlightTexturePath;
    }

}
