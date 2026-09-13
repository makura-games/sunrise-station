using Content.Client.Clickable;
using Content.Client._Sunrise.Sandbox.Access.Overlays;
using Content.Client.Administration.Managers;
using Robust.Client.GameObjects;
using Content.Client.UserInterface.Systems.Sandbox;
using Content.Shared._Sunrise.Misc.Events;
using Content.Shared.Access.Components;
using Content.Shared.Containers;
using Content.Shared.Administration;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Physics.Components;

namespace Content.Client._Sunrise.Sandbox.Access.Systems;
/// <summary>
/// Manages the mapping access overlay and exposes its current UI-facing state.
/// </summary>
public sealed partial class MappingAccessOverlaySystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IClientAdminManager _admin = default!;
    [Dependency] private IClickMapManager _clickMap = default!;
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IResourceCache _resource = default!;
    [Dependency] private EntityQuery<ContainerFillComponent> _containerFillQuery = default!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;

    private MappingAccessOverlay? _overlay;
    private MappingAccessOutlineOverlay? _outlineOverlay;
    private MappingAccessReaderResolver? _readerResolver;
    private MappingAccessTightBounds? _tightBounds;

    /// <summary>
    /// Raised after the overlay state or filter settings change.
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    /// Indicates whether the overlay is currently active.
    /// </summary>
    public bool Enabled { get; private set; }

    /// <summary>
    /// Indicates whether the current user may enable the overlay.
    /// </summary>
    public bool CanEnable => _admin.HasFlag(AdminFlags.Mapping);

    /// <summary>
    /// Gets the active body filter used by the overlay.
    /// </summary>
    public MappingAccessBodyFilter BodyFilter { get; private set; } = MappingAccessBodyFilter.Both;

    /// <summary>
    /// Indicates whether only contained electronics should provide displayed access.
    /// </summary>
    public bool ElectronicsOnly { get; private set; }

    /// <summary>
    /// Creates the overlay instance and starts tracking admin permission changes.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        _admin.AdminStatusUpdated += OnAdminStatusUpdated;

        SubscribeLocalEvent<AccessReaderComponent, ComponentStartup>(OnAccessReaderStartup);
        SubscribeLocalEvent<AccessReaderComponent, ComponentShutdown>(OnAccessReaderShutdown);
        SubscribeLocalEvent<AccessReaderComponent, ComponentRemove>(OnAccessReaderRemove);
        SubscribeLocalEvent<AccessReaderComponent, AccessReaderConfigurationChangedEvent>(OnAccessReaderChanged);
        UpdateUi();

        SubscribeNetworkEvent<ToggleAccessOverlayEvent>(OnToggleOverlay);
    }

    private void OnToggleOverlay(ToggleAccessOverlayEvent ev, EntitySessionEventArgs args)
    {
        TrySetEnabled(!Enabled);
    }

    /// <summary>
    /// Removes the overlay and detaches admin-status listeners.
    /// </summary>
    public override void Shutdown()
    {
        base.Shutdown();

        _admin.AdminStatusUpdated -= OnAdminStatusUpdated;
    }

    private void OnAdminStatusUpdated()
    {
        if (Enabled && !CanSetEnabled(true))
            SetEnabled(false);
        else
        {
            UpdateUi();
            StateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Attempts to enable or disable the overlay.
    /// </summary>
    public bool TrySetEnabled(bool enabled)
    {
        if (Enabled == enabled)
            return true;

        if (!CanSetEnabled(enabled))
            return false;

        SetEnabled(enabled);
        return true;
    }

    /// <summary>
    /// Returns whether the requested enabled state is currently allowed.
    /// </summary>
    public bool CanSetEnabled(bool enabled)
    {
        return !enabled || CanEnable;
    }

    /// <summary>
    /// Updates which body types receive access labels.
    /// </summary>
    public void SetBodyFilter(MappingAccessBodyFilter filter)
    {
        if (BodyFilter == filter)
            return;

        BodyFilter = filter;
        _overlay?.BodyFilter = filter;
        _outlineOverlay?.BodyFilter = filter;
        StateChanged?.Invoke();
    }

    /// <summary>
    /// Toggles whether displayed access should come only from contained electronics.
    /// </summary>
    public void SetElectronicsOnly(bool electronicsOnly)
    {
        if (ElectronicsOnly == electronicsOnly)
            return;

        ElectronicsOnly = electronicsOnly;
        _overlay?.ElectronicsOnly = electronicsOnly;
        _outlineOverlay?.ElectronicsOnly = electronicsOnly;

        if (electronicsOnly)
            _readerResolver?.MarkAccessReaderLookupDirty();

        StateChanged?.Invoke();
    }

    private void OnAccessReaderStartup(Entity<AccessReaderComponent> ent, ref ComponentStartup args)
    {
        _readerResolver?.SyncAccessReaderLookup(ent.Owner, ent.Comp);
    }

    private void OnAccessReaderShutdown(Entity<AccessReaderComponent> ent, ref ComponentShutdown args)
    {
        _readerResolver?.RemoveAccessReaderLookup(ent.Owner);
    }

    private void OnAccessReaderRemove(Entity<AccessReaderComponent> ent, ref ComponentRemove args)
    {
        _readerResolver?.RemoveAccessReaderLookup(ent.Owner);
    }

    private void OnAccessReaderChanged(Entity<AccessReaderComponent> ent, ref AccessReaderConfigurationChangedEvent args)
    {
        _readerResolver?.SyncAccessReaderLookup(ent.Owner, ent.Comp);
    }

    private void SetEnabled(bool enabled)
    {
        Enabled = enabled;

        if (enabled)
        {
            _readerResolver = new(EntityManager, _prototype, _containerFillQuery);
            _readerResolver.MarkAccessReaderLookupDirty();
            _tightBounds = new(_clickMap);
            _overlay = new(EntityManager, _lookup, _sprite, _prototype, Loc, _resource, _ui, _readerResolver, _tightBounds, _physicsQuery);
            _outlineOverlay = new(EntityManager, _sprite, _prototype, _clyde, _readerResolver, _tightBounds, _physicsQuery);
            _overlay.BodyFilter = BodyFilter;
            _overlay.ElectronicsOnly = ElectronicsOnly;
            _outlineOverlay.BodyFilter = BodyFilter;
            _outlineOverlay.ElectronicsOnly = ElectronicsOnly;

            if (!_overlayManager.HasOverlay<MappingAccessOverlay>())
                _overlayManager.AddOverlay(_overlay);

            if (!_overlayManager.HasOverlay<MappingAccessOutlineOverlay>())
                _overlayManager.AddOverlay(_outlineOverlay);
        }
        else
        {
            if (_overlay != null)
                _overlayManager.RemoveOverlay(_overlay);

            if (_outlineOverlay != null)
                _overlayManager.RemoveOverlay(_outlineOverlay);

            _overlay?.Dispose();
            _outlineOverlay?.Dispose();
            _readerResolver?.Dispose();
            _tightBounds?.ClearCache();

            _overlay = null;
            _outlineOverlay = null;
            _readerResolver = null;
            _tightBounds = null;
        }

        UpdateUi();
        StateChanged?.Invoke();
    }

    private void UpdateUi()
    {
        var controller = _ui.GetUIController<SandboxUIController>();
        controller.SetMappingAccessVisible(CanEnable);
        controller.SetToggleMappingAccess(Enabled);
    }
}
