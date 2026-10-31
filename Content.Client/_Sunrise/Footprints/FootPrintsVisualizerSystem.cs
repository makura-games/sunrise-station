using Content.Client.Fluids;
using Content.Shared._Sunrise.Footprints;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Sunrise.Footprints;

/// <summary>
/// Handles the visual appearance and updates of footprint entities on the client
/// </summary>
public sealed partial class FootprintVisualizerSystem : EntitySystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FootprintComponent, ComponentInit>(OnFootprintInitialized);
        SubscribeLocalEvent<FootprintComponent, ComponentShutdown>(OnFootprintShutdown);

        // Причина по которой нельзя использовать VisualizerSystem<T>
        SubscribeLocalEvent<FootprintComponent, AppearanceChangeEvent>(OnAppearanceChange, after:[typeof(PuddleSystem)]);
    }

    private void OnAppearanceChange(EntityUid uid, FootprintComponent component, ref AppearanceChangeEvent args)
    {
        UpdateFootprintVisuals((uid, component));
    }

    /// <summary>
    /// Initializes the visual appearance of a new footprint
    /// </summary>
    private void OnFootprintInitialized(Entity<FootprintComponent> ent, ref ComponentInit args)
    {
        InitializeSpriteLayers(ent);
        UpdateFootprintVisuals(ent);
    }

    /// <summary>
    /// Cleans up the visual elements when a footprint is removed
    /// </summary>
    private void OnFootprintShutdown(Entity<FootprintComponent> ent, ref ComponentShutdown args)
    {
        RemoveSpriteLayers(ent);
    }

    /// <summary>
    /// Sets up the initial sprite layers for the footprint
    /// </summary>
    private void InitializeSpriteLayers(EntityUid uid)
    {
        _sprite.LayerMapReserve(uid, FootprintSpriteLayer.MainLayer);
    }

    /// <summary>
    /// Removes sprite layers when cleaning up footprint
    /// </summary>
    private void RemoveSpriteLayers(EntityUid uid)
    {
        _sprite.LayerMapRemove(uid, FootprintSpriteLayer.MainLayer);
    }

    /// <summary>
    /// Updates the visual appearance of a footprint based on its current state
    /// </summary>
    private void UpdateFootprintVisuals(Entity<FootprintComponent> ent)
    {
        if (!_sprite.LayerMapTryGet(ent.Owner, FootprintSpriteLayer.MainLayer, out var layer, true))
            return;

        if (!_appearance.TryGetData<string>(ent, FootprintVisualParameter.VisualState, out var visualState))
            return;

        UpdateSpriteState(ent, layer, visualState, ent.Comp.SpritePath);
        UpdateSpriteColor(ent);
    }

    /// <summary>
    /// Updates the sprite state based on the footprint type
    /// </summary>
    private void UpdateSpriteState(EntityUid uid, int layer, string state, ResPath spritePath)
    {
        _sprite.LayerSetRsi(uid, layer, spritePath, state);
    }

    /// <summary>
    /// Updates the sprite color based on appearance data
    /// </summary>
    private void UpdateSpriteColor(EntityUid uid)
    {
        if (!_appearance.TryGetData<Color>(uid, FootprintVisualParameter.TrackColor, out var color))
            return;

        _sprite.SetColor(uid, color);
    }
}
