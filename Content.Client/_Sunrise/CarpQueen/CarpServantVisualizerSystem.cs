using Content.Shared._Sunrise.CarpQueen;
using Content.Shared.Light.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client._Sunrise.CarpQueen;

/// <summary>
/// Client-side system that applies the liquid color to carp servants
/// based on the color of the liquid they hatched from.
/// Overrides RgbLightController behavior to use fixed color.
/// </summary>
public sealed partial class CarpServantVisualizerSystem : VisualizerSystem<CarpServantMemoryComponent>
{
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CarpServantMemoryComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CarpServantMemoryComponent, ComponentAdd>(OnComponentAdd);
    }

    private void OnComponentAdd(EntityUid uid, CarpServantMemoryComponent component, ComponentAdd args)
    {
        // Remove RgbLightController on client side to prevent rainbow effect
        RemComp<RgbLightControllerComponent>(uid);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // Continuously override RgbLightController color with fixed liquid color
        var query = EntityQueryEnumerator<CarpServantMemoryComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var memory, out var sprite))
        {
            var color = memory.LiquidColor;
            
            // Override sprite layer 0 color (base layer)
            _sprite.LayerSetColor((uid, sprite), 0, color);
            
            // Override light color if present
            if (TryComp<PointLightComponent>(uid, out var light))
            {
                _lights.SetColor(uid, color, light);
            }
        }
    }

    protected override void OnAppearanceChange(EntityUid uid, CarpServantMemoryComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        // Apply color to sprite layers
        var color = component.LiquidColor;
        var sprite = args.Sprite;
        
        // Apply color to base layer (layer 0)
        _sprite.LayerSetColor((uid, sprite), 0, color);
        
        // Also update light color if present
        if (TryComp<PointLightComponent>(uid, out var light))
        {
            _lights.SetColor(uid, color, light);
        }
    }

    private void OnStartup(EntityUid uid, CarpServantMemoryComponent component, ComponentStartup args)
    {
        // Apply color immediately on startup
        if (TryComp<SpriteComponent>(uid, out var sprite))
        {
            _sprite.LayerSetColor((uid, sprite), 0, component.LiquidColor);
        }
        
        if (TryComp<PointLightComponent>(uid, out var light))
        {
            _lights.SetColor(uid, component.LiquidColor, light);
        }
    }
}

