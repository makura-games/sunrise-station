// © SUNRISE, An EULA/CLA with a hosting restriction, full text: https://github.com/makura-games/sunrise-station/blob/master/CLA.txt
using Content.Shared._Sunrise.GhostTheme;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.GhostTheme;

public sealed partial class GhostThemeSystem : EntitySystem
{
    private const string UpstreamGhostLayer = "ghostVariant";

    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GhostThemeComponent, AfterAutoHandleStateEvent>(OnInit);
        SubscribeLocalEvent<GhostThemeComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInit(Entity<GhostThemeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (ent.Comp.GhostTheme == null ||
            !_proto.TryIndex<GhostThemePrototype>(ent.Comp.GhostTheme, out var ghostTheme) ||
            ghostTheme.UseUpstreamSprite)
        {
            RestoreUpstreamSprite((ent, sprite));
            return;
        }

        var spriteEnt = (ent, sprite);
        var layer = _sprite.LayerMapReserve(spriteEnt, GhostThemeVisualLayers.Theme);

        _sprite.LayerSetSprite(spriteEnt, layer, ghostTheme.Sprite);
        sprite.LayerSetShader(layer, "unshaded");
        _sprite.LayerSetColor(spriteEnt, layer, ghostTheme.SpriteColor);
        _sprite.LayerSetScale(spriteEnt, layer, ghostTheme.Scale);
        _sprite.LayerSetVisible(spriteEnt, layer, true);

        if (_sprite.LayerMapTryGet(spriteEnt, UpstreamGhostLayer, out var upstreamLayer, false))
            _sprite.LayerSetVisible(spriteEnt, upstreamLayer, false);
    }

    private void OnShutdown(Entity<GhostThemeComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            RestoreUpstreamSprite((ent, sprite));
    }

    private void RestoreUpstreamSprite(Entity<SpriteComponent?> ent)
    {
        _sprite.RemoveLayer(ent, GhostThemeVisualLayers.Theme, false);

        if (_sprite.LayerMapTryGet(ent, UpstreamGhostLayer, out var upstreamLayer, false))
            _sprite.LayerSetVisible(ent, upstreamLayer, true);
    }

    private enum GhostThemeVisualLayers : byte
    {
        Theme,
    }
}
