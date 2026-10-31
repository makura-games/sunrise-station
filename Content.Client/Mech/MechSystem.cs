using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.Mech;

/// <inheritdoc/>
public sealed partial class MechSystem : SharedMechSystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechComponent, AppearanceChangeEvent>(OnAppearanceChanged);
    }

    private void OnAppearanceChanged(EntityUid uid, MechComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        UpdateAppearance(uid, component, args.Sprite);
    }
    private void UpdateAppearance(EntityUid uid, MechComponent component, SpriteComponent sprite)
    {
        var state = component.BaseState;
        var drawDepth = DrawDepth.Mobs;
        UpdatePaintApperance(uid, component);

        if (component.BrokenState != null
            && _appearance.TryGetData<bool>(uid, MechVisuals.Broken, out var broken)
            && broken)
        {
            state = component.BrokenState;
            drawDepth = DrawDepth.SmallMobs;
        }
        else if (component.OpenState != null
                 && _appearance.TryGetData<bool>(uid, MechVisuals.Open, out var open)
                 && open)
        {
            state = component.OpenState;
            drawDepth = DrawDepth.SmallMobs;
        }

        _sprite.LayerSetRsiState((uid, sprite), MechVisualLayers.Base, state);
        _sprite.SetDrawDepth((uid, sprite), (int)drawDepth);
    }

    private void UpdatePaintApperance(EntityUid uid, MechComponent component)
    {
        if (_appearance.TryGetData<string>(uid, MechVisualLayers.Base, out var baseState))
            component.BaseState = baseState;

        if (_appearance.TryGetData<string>(uid, MechVisualLayers.Open, out var open))
            component.OpenState = open;

        if (_appearance.TryGetData<string>(uid, MechVisualLayers.Broken, out var broken))
            component.BrokenState = broken;
    }
}
