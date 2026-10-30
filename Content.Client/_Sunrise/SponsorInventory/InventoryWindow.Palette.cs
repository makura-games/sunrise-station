using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Inventory.Controls;
using Content.Shared._Sunrise.SponsorInventory;
using Content.Shared.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Preferences.Loadouts;
using Content.Sunrise.Interfaces.Shared;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.SponsorInventory;

public sealed partial class InventoryWindow
{
    /*
     * Palette building, filtering, activation, and drag/drop behavior for loadout and sponsor entries.
     */
    private void BuildPaletteEntries(RoleLoadout roleLoadout)
    {
        _paletteEntries.Clear();

        var jobId = CurrentJobId;
        if (jobId != null)
            AddLoadoutPaletteEntries(jobId, roleLoadout);

        AddSponsorPaletteEntries();
    }

    private void AddLoadoutPaletteEntries(string jobId, RoleLoadout roleLoadout)
    {
        var jobLoadoutId = LoadoutSystem.GetJobPrototype(jobId);
        var effectiveJobLoadoutId = LoadoutSystem.GetEffectiveRolePrototype(jobLoadoutId, _prototype);
        if (!_prototype.TryIndex<RoleLoadoutPrototype>(effectiveJobLoadoutId, out var roleLoadoutProto))
            return;

        var loadoutSystem = _entManager.System<LoadoutSystem>();
        var collection = IoCManager.Instance!;
        var sponsorConfig = _sponsorInventory.GetSponsorInventoryConfig();
        var purchasedSponsorItems = _sponsorInventory.GetPurchasedInventoryItems().ToHashSet();
        var sponsorTier = _sponsorInventory.GetSponsorTier();
        var sponsorEntitlements = _sponsorInventory.GetSponsorInventoryEntitlements().ToHashSet();

        // Group metadata is copied onto each entry so the palette can sort and show limits without re-reading prototypes.
        for (var groupOrder = 0; groupOrder < roleLoadoutProto.Groups.Count; groupOrder++)
        {
            var groupId = roleLoadoutProto.Groups[groupOrder];
            if (!_prototype.TryIndex(groupId, out LoadoutGroupPrototype? group) || group.Hidden)
                continue;

            if (!roleLoadout.SelectedLoadouts.ContainsKey(group.ID))
                roleLoadout.SelectedLoadouts[group.ID] = new List<Loadout>();

            var selectedLoadouts = roleLoadout.SelectedLoadouts[group.ID];
            var selectedCount = selectedLoadouts.Count;
            var groupMinLimit = Math.Max(0, group.MinLimit);
            var groupMaxLimit = group.MaxLimit > 0 ? group.MaxLimit : (int?)null;
            var groupName = Loc.GetString(group.Name);

            foreach (var loadoutId in group.Loadouts)
            {
                if (!_prototype.TryIndex(loadoutId, out LoadoutPrototype? loadout))
                    continue;

                var selected = selectedLoadouts.Any(l => l.Prototype == loadout.ID);
                FormattedMessage? reason = null;
                var enabled = _characterProfile != null && roleLoadout.IsValid(
                    _characterProfile,
                    _player.LocalSession,
                    loadout.ID,
                    collection,
                    out reason);

                // Sunrise-Edit: проверяем UI по актуальному снимку каталога и владения из SunriseInventorySystem.
                if (enabled && !SunriseInventoryValidation.CanUseLoadout(
                        loadout,
                        _prototype,
                        sponsorConfig,
                        purchasedSponsorItems,
                        sponsorTier,
                        sponsorEntitlements))
                {
                    enabled = false;
                    reason = FormattedMessage.FromUnformatted(
                        Loc.GetString("sunrise-inventory-item-unavailable"));
                }

                var equipment = GetLoadoutEquipment(loadout);
                var storage = GetLoadoutStorage(loadout);
                var icon = GetLoadoutIcon(loadout, equipment, storage);
                if (icon == null)
                    continue;

                var bagPrototypes = storage.SelectMany(p => p.Value).Select(p => p.Id).ToList();
                _paletteEntries.Add(new InventoryPaletteEntry(
                    InventoryPaletteEntrySource.Loadout,
                    loadout.ID,
                    loadoutSystem.GetName(loadout),
                    icon,
                    GetEntityDescription(icon),
                    GetLoadoutRequirements(enabled, reason),
                    GetEquipmentTargetSlots(equipment),
                    bagPrototypes,
                    bagPrototypes.Count > 0,
                    group.ID,
                    groupName,
                    groupOrder,
                    selectedCount,
                    groupMinLimit,
                    groupMaxLimit,
                    loadout.ID,
                    null,
                    !selected || selectedCount > groupMinLimit,
                    enabled,
                    selected));
            }
        }
    }

    private void AddSponsorPaletteEntries()
    {
        var config = _sponsorInventory.GetSponsorInventoryConfig();

        var selection = GetSponsorSelectionSnapshot();
        var slotCache = new Dictionary<string, HashSet<string>>();
        var bagFitCache = new Dictionary<string, bool>();

        // Slot and bag-fit checks spawn probe entities, so cache by prototype/item for this palette rebuild.
        var sponsorItems = new List<SponsorInventoryItemInfo>();
        foreach (var sponsorItem in config.Items ?? [])
        {
            if (sponsorItem == null)
                continue;

            if (string.IsNullOrWhiteSpace(sponsorItem.Id))
                continue;

            if (string.IsNullOrWhiteSpace(sponsorItem.EntityPrototype))
                continue;

            if (!_prototype.HasIndex<EntityPrototype>(sponsorItem.EntityPrototype))
                continue;

            sponsorItems.Add(sponsorItem);
        }

        sponsorItems.Sort((left, right) => string.Compare(
            GetSponsorItemName(left),
            GetSponsorItemName(right),
            StringComparison.CurrentCulture));

        foreach (var sponsorItem in sponsorItems)
        {
            var canUse = _sponsorInventory.CanUseItem(sponsorItem.Id, CurrentJobId);

            if (!slotCache.TryGetValue(sponsorItem.EntityPrototype, out var targetSlots))
            {
                targetSlots = GetValidSlotsForEntityPrototype(sponsorItem.EntityPrototype);
                slotCache[sponsorItem.EntityPrototype] = targetSlots;
            }

            var selected = IsSponsorItemSelected(selection, sponsorItem.Id);
            var selectedInBag = selection.BagItems.Contains(sponsorItem.Id);

            if (!bagFitCache.TryGetValue(sponsorItem.Id, out var canFitBag))
            {
                canFitBag = CanAddSponsorItemToBag(sponsorItem.Id);
                bagFitCache[sponsorItem.Id] = canFitBag;
            }
            var canPlaceInBag = selectedInBag || canFitBag;

            var enabled = canUse && (targetSlots.Count > 0 || canPlaceInBag);
            var reason = !canUse
                ? Loc.GetString("sunrise-inventory-item-unavailable")
                : enabled
                    ? null
                    : Loc.GetString("sunrise-inventory-item-no-placement");

            _paletteEntries.Add(new InventoryPaletteEntry(
                InventoryPaletteEntrySource.Sponsor,
                sponsorItem.Id,
                GetSponsorItemName(sponsorItem),
                sponsorItem.EntityPrototype,
                GetEntityDescription(sponsorItem.EntityPrototype),
                GetSponsorRequirements(sponsorItem, reason, canUse && !enabled),
                targetSlots,
                new List<string> { sponsorItem.EntityPrototype },
                canPlaceInBag,
                null,
                null,
                int.MaxValue,
                0,
                0,
                null,
                null,
                sponsorItem.Id,
                true,
                enabled,
                selected));
        }
    }

    private void BuildPlacementOptions(List<SlotDefinition> slots)
    {
        var previous = CurrentPlacementFilter();
        PlacementFilter.Clear();
        _placementOptions.Clear();

        // The bag editor forces a synthetic placement filter because only backpack-compatible entries are actionable there.
        if (_bagEditorOpen)
        {
            AddPlacementOption(Loc.GetString("sunrise-inventory-filter-placement-bag"), PlacementFilterBag);
            _placementFilter = 0;
            PlacementFilter.SelectId(_placementFilter);
            return;
        }

        AddPlacementOption(Loc.GetString("sunrise-inventory-filter-placement-slot"), PlacementFilterAnySlot);

        // Specific slot filters share the same option list as synthetic filters, so the backing value is the slot id.
        foreach (var slot in slots)
        {
            AddPlacementOption(
                Loc.GetString("sunrise-inventory-filter-placement-slot-named", ("slot", GetSlotLabel(slot))),
                slot.Name);
        }

        var selected = _selectedEquipmentSlot != null
            ? _placementOptions.IndexOf(_selectedEquipmentSlot)
            : _placementOptions.IndexOf(previous);

        _placementFilter = selected >= 0 ? selected : 0;
        PlacementFilter.SelectId(_placementFilter);
    }

    private void AddPlacementOption(string text, string value)
    {
        var id = _placementOptions.Count;
        _placementOptions.Add(value);
        PlacementFilter.AddItem(text, id);
    }

    private string CurrentPlacementFilter()
    {
        if (_placementFilter < 0 || _placementFilter >= _placementOptions.Count)
            return PlacementFilterAll;

        return _placementOptions[_placementFilter];
    }

    private static bool IsSpecificSlotFilter(string placementFilter)
    {
        return placementFilter != PlacementFilterAll &&
               placementFilter != PlacementFilterBag &&
               placementFilter != PlacementFilterAnySlot;
    }

    private void UpdateBagEditorControls(bool enabled)
    {
        BagEditorButton.Disabled = !enabled;
        BagEditorButton.Pressed = _bagEditorOpen;
        BagEditorButton.Text = Loc.GetString(_bagEditorOpen
            ? "sunrise-inventory-bag-close-editor"
            : "sunrise-inventory-bag-edit");
        PaletteTitle.Text = Loc.GetString(_bagEditorOpen
            ? "sunrise-inventory-bag-editor-title"
            : "sunrise-inventory-palette-title");
        SourceFilterLabel.Visible = _bagEditorOpen;
        PlacementFilter.Visible = !_bagEditorOpen;
    }

    private void RefreshPalette()
    {
        ItemPalette.RemoveAllChildren();
        UpdateBagEditorControls(_characterProfile != null && CurrentJobId != null);

        var filter = (InventoryPaletteSourceFilter)_sourceFilter;
        var placementFilter = _bagEditorOpen
            ? PlacementFilterBag
            : _selectedEquipmentSlot ?? CurrentPlacementFilter();
        var search = ItemSearch.Text.Trim();

        RefreshPaletteStatus();

        // Enabled entries stay first, then mode-specific placement/source ordering keeps likely actions near the top.
        var entries = _paletteEntries
            .Where(e => filter == InventoryPaletteSourceFilter.All ||
                        filter == InventoryPaletteSourceFilter.Loadout && e.Source == InventoryPaletteEntrySource.Loadout ||
                        filter == InventoryPaletteSourceFilter.Sponsor && e.Source == InventoryPaletteEntrySource.Sponsor)
            .Where(e => MatchesPlacementFilter(e, placementFilter))
            .Where(e => string.IsNullOrWhiteSpace(search) ||
                        e.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.Enabled ? 0 : 1)
            .ThenBy(e => _bagEditorOpen ? e.GroupOrder : GetPlacementSort(e, placementFilter))
            .ThenBy(e => GetAnySlotSort(e, placementFilter))
            .ThenBy(e => GetAnySlotNameSort(e, placementFilter))
            .ThenBy(e => GetSourceSort(e, filter, placementFilter))
            .ThenBy(e => e.Name)
            .ToList();

        if (entries.Count == 0)
        {
            AddPaletteInfo(_bagEditorOpen
                ? "sunrise-inventory-bag-editor-empty"
                : _selectedEquipmentSlot == null
                ? "sunrise-inventory-palette-empty"
                : "sunrise-inventory-slot-no-items");
            return;
        }

        if (_bagEditorOpen)
        {
            RenderBackpackPalette(entries, filter == InventoryPaletteSourceFilter.All);
            return;
        }

        RenderRegularPalette(entries);
    }

    private void RefreshPaletteStatus()
    {
        if (_bagEditorOpen)
        {
            StatusLabel.Text = Loc.GetString("sunrise-inventory-bag-editor-status");

            if (TryGetPreviewBackStorage(out _, out _))
                BagHint.Text = Loc.GetString("sunrise-inventory-bag-editor-hint");

            return;
        }

        if (_selectedEquipmentSlot == null)
        {
            StatusLabel.Text = string.Empty;
            return;
        }

        StatusLabel.Text = Loc.GetString(
            "sunrise-inventory-selected-slot",
            ("slot", GetSlotLabel(_selectedEquipmentSlot)));
    }

    private void RenderRegularPalette(List<InventoryPaletteEntry> entries)
    {
        var grid = CreatePaletteGrid();
        ItemPalette.AddChild(grid);

        foreach (var entry in entries)
        {
            AddPaletteEntryControl(grid, entry);
        }
    }

    private void RenderBackpackPalette(List<InventoryPaletteEntry> entries, bool sponsorsFirst)
    {
        var sponsorEntries = entries
            .Where(e => e.Source == InventoryPaletteEntrySource.Sponsor)
            .ToList();

        // Bag editor groups loadout entries by loadout group so group min/max limits stay visible while packing storage.
        if (sponsorsFirst && sponsorEntries.Count > 0)
            AddBackpackPaletteGroup(Loc.GetString("sunrise-inventory-bag-group-sponsor"), sponsorEntries);

        foreach (var group in entries
                     .Where(e => e.Source == InventoryPaletteEntrySource.Loadout)
                     .GroupBy(e => e.GroupId ?? e.Id)
                     .OrderBy(g => g.Min(e => e.GroupOrder))
                     .ThenBy(g => g.First().GroupName ?? string.Empty))
        {
            AddBackpackPaletteGroup(GetBackpackGroupHeading(group.First()), group);
        }

        if (!sponsorsFirst && sponsorEntries.Count > 0)
            AddBackpackPaletteGroup(Loc.GetString("sunrise-inventory-bag-group-sponsor"), sponsorEntries);
    }

    private void AddBackpackPaletteGroup(string heading, IEnumerable<InventoryPaletteEntry> entries)
    {
        ItemPalette.AddChild(new Label
        {
            Text = heading,
            StyleClasses = { StyleClass.LabelHeading },
            Margin = new Thickness(4, 6, 4, 2),
        });

        var grid = CreatePaletteGrid();
        ItemPalette.AddChild(grid);

        foreach (var entry in entries)
        {
            AddPaletteEntryControl(grid, entry);
        }
    }

    private static string GetBackpackGroupHeading(InventoryPaletteEntry entry)
    {
        var groupName = entry.GroupName ?? Loc.GetString("sunrise-inventory-bag-group-loadout");
        return entry.GroupMaxLimit == null
            ? groupName
            : Loc.GetString(
                "sunrise-inventory-bag-group-limit",
                ("group", groupName),
                ("selected", entry.GroupSelectedCount),
                ("max", entry.GroupMaxLimit.Value));
    }

    private static GridContainer CreatePaletteGrid()
    {
        return new GridContainer
        {
            Columns = 4,
            HSeparationOverride = 0,
            VSeparationOverride = 0,
            HorizontalExpand = true,
        };
    }

    private void AddPaletteEntryControl(GridContainer grid, InventoryPaletteEntry entry)
    {
        var selected = IsEntrySelectedInCurrentContext(entry);
        var control = new InventoryPaletteItemControl(entry, selected, BuildPaletteTooltip(entry));

        // Mouse down/up are used for drag detection, while OnPressed keeps normal click activation working.
        control.OnKeyBindDown += args => OnPaletteKeyDown(args, control);
        control.OnKeyBindUp += args => OnPaletteKeyUp(args, control);
        control.OnPressed += _ => ActivateInventoryPaletteEntry(control.Entry);
        grid.AddChild(control);
    }

    private static bool MatchesPlacementFilter(InventoryPaletteEntry entry, string placementFilter)
    {
        return placementFilter switch
        {
            PlacementFilterAll => true,
            PlacementFilterBag => entry.CanPlaceInBag && entry.BagPrototypes.Count > 0,
            PlacementFilterAnySlot => entry.TargetSlots.Count > 0,
            _ => entry.TargetSlots.Contains(placementFilter),
        };
    }

    private static int GetPlacementSort(InventoryPaletteEntry entry, string placementFilter)
    {
        if (placementFilter == PlacementFilterBag)
            return entry.CanPlaceInBag ? 0 : 1;

        if (placementFilter != PlacementFilterAll && placementFilter != PlacementFilterAnySlot)
            return entry.TargetSlots.Contains(placementFilter) ? 0 : 1;

        if (entry.TargetSlots.Count > 0)
            return 0;

        return entry.CanPlaceInBag ? 1 : 2;
    }

    private int GetAnySlotSort(InventoryPaletteEntry entry, string placementFilter)
    {
        if (placementFilter != PlacementFilterAnySlot)
            return 0;

        var best = int.MaxValue;
        foreach (var slot in entry.TargetSlots)
        {
            var index = _placementOptions.IndexOf(slot);
            if (index >= 0 && index < best)
                best = index;
        }

        return best;
    }

    private string GetAnySlotNameSort(InventoryPaletteEntry entry, string placementFilter)
    {
        if (placementFilter != PlacementFilterAnySlot)
            return string.Empty;

        return entry.TargetSlots
            .OrderBy(GetAnySlotOrder)
            .ThenBy(GetSlotLabel)
            .FirstOrDefault() ?? string.Empty;
    }

    private int GetAnySlotOrder(string slot)
    {
        var index = _placementOptions.IndexOf(slot);
        return index >= 0 ? index : int.MaxValue;
    }

    private static int GetSourceSort(
        InventoryPaletteEntry entry,
        InventoryPaletteSourceFilter filter,
        string placementFilter)
    {
        if (placementFilter == PlacementFilterAnySlot)
            return 0;

        if (filter == InventoryPaletteSourceFilter.All)
            return entry.Source == InventoryPaletteEntrySource.Sponsor ? 0 : 1;

        return (int)entry.Source;
    }

    private bool IsEntrySelectedInCurrentContext(InventoryPaletteEntry entry)
    {
        if (_selectedEquipmentSlot == null)
            return entry.Selected;

        return IsEntrySelectedForSlot(entry, _selectedEquipmentSlot);
    }

    private bool IsEntrySelectedForSlot(InventoryPaletteEntry entry, string slot)
    {
        var selection = GetSponsorSelectionSnapshot();

        if (selection.SlotItems.TryGetValue(slot, out var selectedSponsor))
            return entry.SponsorItemId != null && selectedSponsor == entry.SponsorItemId;

        return entry.Source == InventoryPaletteEntrySource.Loadout &&
               entry.LoadoutId != null &&
               TryFindSelectedLoadoutSlot(slot, out _, out var selectedLoadout) &&
               selectedLoadout == entry.LoadoutId;
    }

    private void OnPaletteKeyDown(GUIBoundKeyEventArgs args, InventoryPaletteItemControl control)
    {
        if (args.Function != EngineKeyFunctions.UIClick || !control.Entry.Enabled)
            return;

        _dragHelper.MouseDown(control);
        args.Handle();
    }

    private void OnPaletteKeyUp(GUIBoundKeyEventArgs args, InventoryPaletteItemControl control)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        if (_dragHelper.IsDragging)
        {
            TryDropInventoryPaletteEntry(control.Entry, args.PointerLocation);
            args.Handle();
        }

        _dragHelper.EndDrag();
    }

    private void ActivateInventoryPaletteEntry(InventoryPaletteEntry entry)
    {
        if (!entry.Enabled)
            return;

        if (_selectedEquipmentSlot != null && entry.TargetSlots.Contains(_selectedEquipmentSlot))
        {
            if (IsEntrySelectedForSlot(entry, _selectedEquipmentSlot))
            {
                RemoveSlotItem(_selectedEquipmentSlot);
                return;
            }

            ApplyInventoryPaletteEntryToSlot(entry, _selectedEquipmentSlot);
            return;
        }

        if (entry.Source == InventoryPaletteEntrySource.Loadout)
        {
            if (entry.Selected && !entry.CanRemove)
            {
                StatusLabel.Text = Loc.GetString("sunrise-inventory-loadout-required");
                return;
            }

            ToggleLoadout(entry, !entry.Selected, GetImplicitReplacementSlot(entry));
            return;
        }

        if (entry.SponsorItemId == null)
            return;

        if (entry.Selected)
        {
            RemoveSponsorItem(entry.SponsorItemId);
            return;
        }

        if (_bagEditorOpen && entry.CanPlaceInBag)
        {
            AddSponsorItemToBag(entry.SponsorItemId);
            return;
        }

        StatusLabel.Text = entry.TargetSlots.Count > 0
            ? Loc.GetString("sunrise-inventory-item-select-slot")
            : Loc.GetString("sunrise-inventory-bag-full");
    }

    private bool OnBeginDrag()
    {
        if (_dragHelper.Dragged is not { } dragged || !dragged.Entry.Enabled)
            return false;

        ClearDragGhost();
        // The drag preview lives in PopupRoot so it can follow the cursor outside the palette scroll container.
        _dragGhost = new EntityPrototypeView
        {
            SetSize = new Vector2(64, 64),
            Scale = new Vector2(2, 2),
            OverrideDirection = Direction.South,
            MouseFilter = MouseFilterMode.Ignore,
        };
        _dragGhost.SetPrototype(dragged.Entry.IconPrototype);
        _ui.PopupRoot.AddChild(_dragGhost);
        MoveDragGhost();
        return true;
    }

    private bool OnContinueDrag(float frameTime)
    {
        if (_dragGhost == null)
            return false;

        MoveDragGhost();
        return true;
    }

    private void OnEndDrag()
    {
        ClearDragGhost();
    }

    private void MoveDragGhost()
    {
        if (_dragGhost == null)
            return;

        LayoutContainer.SetPosition(_dragGhost, _ui.MousePositionScaled.Position - new Vector2(32, 32));
    }

    private void TryDropInventoryPaletteEntry(InventoryPaletteEntry entry, ScreenCoordinates pointer)
    {
        var target = _ui.MouseGetControl(pointer);

        // Drop targets are resolved by walking the UI parent chain, so children still count as their slot or bag.
        if (TryFindParentSlot(target, out var slot))
        {
            ApplyInventoryPaletteEntryToSlot(entry, slot);
            return;
        }

        if (!IsInsideBag(target))
            return;

        if (!_bagEditorOpen)
        {
            StatusLabel.Text = Loc.GetString("sunrise-inventory-bag-editor-open-first");
            return;
        }

        if (entry.SponsorItemId != null)
        {
            AddSponsorItemToBag(entry.SponsorItemId);
            return;
        }

        if (entry.Source == InventoryPaletteEntrySource.Loadout && entry.BagPrototypes.Count > 0)
            ToggleLoadout(entry, true);
    }

    private bool TryFindParentSlot(Control? control, out string slot)
    {
        while (control != null)
        {
            if (control is SlotControl slotControl && _slotControls.TryGetValue(slotControl, out var slotName))
            {
                slot = slotName;
                return true;
            }

            control = control.Parent;
        }

        slot = string.Empty;
        return false;
    }

    private bool IsInsideBag(Control? control)
    {
        while (control != null)
        {
            if (control == BagGridHost)
                return true;

            control = control.Parent;
        }

        return false;
    }

    private void ApplyInventoryPaletteEntryToSlot(InventoryPaletteEntry entry, string slot)
    {
        if (!entry.Enabled || !entry.TargetSlots.Contains(slot))
            return;

        if (entry.Source == InventoryPaletteEntrySource.Loadout)
        {
            ToggleLoadout(entry, true, slot, slot);
            return;
        }

        if (entry.SponsorItemId == null)
            return;

        AddSponsorItemToSlot(entry.SponsorItemId, slot);
    }

    private bool ToggleLoadout(
        InventoryPaletteEntry entry,
        bool selected,
        string? replacementSlot = null,
        string? sponsorSlotToClear = null)
    {
        if (_characterProfile == null)
            return false;

        if (entry.GroupId == null || entry.LoadoutId == null)
            return false;

        if (CurrentJobId == null)
            return false;

        var roleLoadout = EnsureRoleLoadout(CurrentJobId);

        if (!roleLoadout.SelectedLoadouts.ContainsKey(entry.GroupId))
            roleLoadout.SelectedLoadouts[entry.GroupId] = new List<Loadout>();

        var alreadySelected = roleLoadout.SelectedLoadouts[entry.GroupId].Any(l => l.Prototype == entry.LoadoutId);
        if (selected && !alreadySelected)
        {
            if (replacementSlot != null &&
                !TryClearLoadoutSlot(roleLoadout, replacementSlot, entry.GroupId, entry.LoadoutId))
            {
                return false;
            }

            if (!TryEnsureLoadoutGroupCapacity(roleLoadout, entry))
                return false;

            roleLoadout.AddLoadout(entry.GroupId, entry.LoadoutId, _prototype);
        }
        else if (!selected && alreadySelected)
        {
            if (!entry.CanRemove)
            {
                StatusLabel.Text = Loc.GetString("sunrise-inventory-loadout-required");
                return false;
            }

            roleLoadout.RemoveLoadout(entry.GroupId, entry.LoadoutId, _prototype);
        }

        if (sponsorSlotToClear != null)
            RemoveSponsorSlotSelection(sponsorSlotToClear);

        // Rebuild after every successful mutation because slot contents, bag fit, and palette availability are interdependent.
        _characterProfile = _characterProfile.WithLoadout(roleLoadout);
        RefreshEquipment();
        return true;
    }

    private string? GetImplicitReplacementSlot(InventoryPaletteEntry entry)
    {
        if (_bagEditorOpen)
            return null;

        // A single-slot loadout can replace its current occupant without forcing the user to select the slot first.
        if (_selectedEquipmentSlot != null && entry.TargetSlots.Contains(_selectedEquipmentSlot))
            return _selectedEquipmentSlot;

        return entry.TargetSlots.Count == 1
            ? entry.TargetSlots.First()
            : null;
    }

    private bool TryClearLoadoutSlot(
        RoleLoadout roleLoadout,
        string slot,
        string selectedGroup,
        string selectedLoadout)
    {
        foreach (var (groupId, selectedLoadouts) in roleLoadout.SelectedLoadouts)
        {
            for (var i = selectedLoadouts.Count - 1; i >= 0; i--)
            {
                var current = selectedLoadouts[i];
                if (groupId == selectedGroup && current.Prototype == selectedLoadout)
                    continue;

                if (!_prototype.TryIndex(current.Prototype, out LoadoutPrototype? loadout) ||
                    !GetLoadoutEquipment(loadout).ContainsKey(slot))
                {
                    continue;
                }

                if (groupId != selectedGroup && !CanRemoveSelectedLoadout(roleLoadout, groupId, current.Prototype))
                {
                    StatusLabel.Text = Loc.GetString("sunrise-inventory-loadout-required");
                    return false;
                }

                // Loadout equipment is exclusive per slot in this UI, so conflicting selected loadouts are removed first.
                selectedLoadouts.RemoveAt(i);
            }
        }

        return true;
    }

    private bool TryEnsureLoadoutGroupCapacity(RoleLoadout roleLoadout, InventoryPaletteEntry entry)
    {
        if (entry.GroupId == null)
            return false;

        if (!_prototype.TryIndex(entry.GroupId, out LoadoutGroupPrototype? group) ||
            group.MaxLimit <= 0 ||
            !roleLoadout.SelectedLoadouts.TryGetValue(entry.GroupId, out var selectedLoadouts) ||
            selectedLoadouts.Count < group.MaxLimit)
        {
            return true;
        }

        StatusLabel.Text = Loc.GetString(
            "sunrise-inventory-loadout-group-full",
            ("group", entry.GroupName ?? entry.GroupId),
            ("selected", selectedLoadouts.Count),
            ("max", group.MaxLimit));
        return false;
    }
}
